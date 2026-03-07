using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DungeonGame.Core;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using Google.Protobuf;
using StackExchange.Redis;
using PlayerInfo = DungeonGame.Core.PlayerInfo;

namespace DungeonServer.Infrastructure.Messaging.Rooms;

public sealed class RedisRoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed class RoomChannel
    {
        public ConcurrentDictionary<Guid, Channel<ReadOnlyMemory<byte>>> SubscriberChannels { get; } = new();

        public byte[]? CurrentState { get; set; }

        public SemaphoreSlim LifecycleLock { get; } = new(1, 1);
        public bool IsSubscribedToRedis { get; set; }
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();
    private readonly IConnectionMultiplexer _redis;

    public RedisRoomSubscriptionRegistry(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> SubscribeAsync(
        int subscriberPlayerId,
        int roomId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());

        var connectionId = Guid.NewGuid();

        var subscriberChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        room.SubscriberChannels.TryAdd(connectionId, subscriberChannel);

        if (room.CurrentState != null)
        {
            subscriberChannel.Writer.TryWrite(room.CurrentState);
        }

        await room.LifecycleLock.WaitAsync(ct);
        try
        {
            if (!room.IsSubscribedToRedis)
            {
                await _redis.GetSubscriber()
                    .SubscribeAsync(
                        RedisChannel.Literal($"room:{roomId}"),
                        (_, value) => HandleRedisMessage(room, value));

                room.IsSubscribedToRedis = true;
            }
        }
        finally
        {
            room.LifecycleLock.Release();
        }

        try
        {
            await foreach (ReadOnlyMemory<byte> roomUpdate in subscriberChannel.Reader.ReadAllAsync(ct))
            {
                yield return roomUpdate;
            }
        }
        finally
        {
            room.SubscriberChannels.TryRemove(connectionId, out _);

            await room.LifecycleLock.WaitAsync(CancellationToken.None);
            try
            {
                if (room.SubscriberChannels.IsEmpty && room.IsSubscribedToRedis)
                {
                    await _redis.GetSubscriber().UnsubscribeAsync(RedisChannel.Literal($"room:{roomId}"));
                    room.IsSubscribedToRedis = false;
                    _rooms.TryRemove(roomId, out _);
                }
            }
            finally
            {
                room.LifecycleLock.Release();
            }
        }
    }

    private static void HandleRedisMessage(RoomChannel room, RedisValue value)
    {
        byte[] stableBytes = (byte[])value!; 

        room.CurrentState = stableBytes;

        foreach (var channel in room.SubscriberChannels.Values)
        {
            channel.Writer.TryWrite(stableBytes);
        }
    }

    public async Task PublishUpdateAsync(int roomId, RoomPlayerUpdate roomUpdate, CancellationToken ct)
    {
        RoomSnapshot protoSnapshot = CreateProtoRoomSnapshot(roomId, roomUpdate);
    
        int calculatedSize = protoSnapshot.CalculateSize();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(calculatedSize);
    
        try
        {
            protoSnapshot.WriteTo(new Span<byte>(buffer, 0, calculatedSize));

            var persistentState = new byte[calculatedSize];
            Buffer.BlockCopy(buffer, 0, persistentState, 0, calculatedSize);
        
            RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
            room.CurrentState = persistentState;

            // Await the Redis publish before returning the buffer
            await _redis.GetSubscriber().PublishAsync(
                RedisChannel.Literal($"room:{roomId}"), 
                new ReadOnlyMemory<byte>(buffer, 0, calculatedSize)
            );
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static RoomSnapshot CreateProtoRoomSnapshot(int roomId, RoomPlayerUpdate roomUpdate)
    {
        var protoSnapshot = new RoomSnapshot { RoomId = roomId };
        foreach (PlayerSnapshot player in roomUpdate.Players)
        {
            protoSnapshot.Players.Add(
                new PlayerInfo
                {
                    Id = player.PlayerId,
                    Location = new DungeonGame.Shared.Location { X = player.Location.X, Y = player.Location.Y }
                });
        }

        return protoSnapshot;
    }
}