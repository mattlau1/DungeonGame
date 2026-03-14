using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
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
        // Field for volatile access - enables lock-free thread-safe reads/writes
        public ImmutableList<ChannelWriter<ReadOnlyMemory<byte>>> Subscriptions =
            ImmutableList<ChannelWriter<ReadOnlyMemory<byte>>>.Empty;

        // Field for volatile access - enables lock-free thread-safe reads/writes
        public byte[]? CurrentState;

        public Lock Gate { get; } = new();
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

        var subscriberChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        bool needsSubscribe;
        lock (room.Gate)
        {
            room.Subscriptions = room.Subscriptions.Add(subscriberChannel.Writer);

            byte[]? currentState = Volatile.Read(ref room.CurrentState);
            if (currentState != null)
            {
                subscriberChannel.Writer.TryWrite(currentState);
            }

            needsSubscribe = !room.IsSubscribedToRedis;
            if (needsSubscribe)
            {
                room.IsSubscribedToRedis = true;
            }
        }

        if (needsSubscribe)
        {
            _redis.GetSubscriber()
                .Subscribe(RedisChannel.Literal($"room:{roomId}"), (_, value) => HandleRedisMessage(room, value));
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
            bool needsUnsubscribe;
            lock (room.Gate)
            {
                room.Subscriptions = room.Subscriptions.Remove(subscriberChannel.Writer);

                needsUnsubscribe = room.Subscriptions.IsEmpty && room.IsSubscribedToRedis;
                if (needsUnsubscribe)
                {
                    room.IsSubscribedToRedis = false;
                    _rooms.TryRemove(roomId, out _);
                }
            }

            if (needsUnsubscribe)
            {
                _redis.GetSubscriber().Unsubscribe(RedisChannel.Literal($"room:{roomId}"));
            }
        }
    }

    private static void HandleRedisMessage(RoomChannel room, RedisValue value)
    {
        // Volatile read ensures visibility across CPU cores
        ImmutableList<ChannelWriter<ReadOnlyMemory<byte>>> subs = Volatile.Read(ref room.Subscriptions);

        var data = (byte[])value!;
        
        // Lock-free write - ensures visibility across threads
        Volatile.Write(ref room.CurrentState, data);

        foreach (ChannelWriter<ReadOnlyMemory<byte>> writer in subs)
        {
            writer.TryWrite(data);
        }
    }

    public Task PublishUpdateAsync(int roomId, RoomPlayerUpdate roomUpdate, CancellationToken ct)
    {
        RoomSnapshot protoSnapshot = CreateProtoRoomSnapshot(roomId, roomUpdate);
        int calculatedSize = protoSnapshot.CalculateSize();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(calculatedSize);

        try
        {
            protoSnapshot.WriteTo(new Span<byte>(buffer, 0, calculatedSize));

            // Create persistent state for new joiners
            var persistentState = new byte[calculatedSize];
            Buffer.BlockCopy(buffer, 0, persistentState, 0, calculatedSize);

            RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());

            // Lock-free write - ensures visibility across threads
            Volatile.Write(ref room.CurrentState, persistentState);

            // Non-blocking background publish
            // ReSharper disable once InconsistentlySynchronizedField
            Task<long> publishTask = _redis.GetSubscriber()
                .PublishAsync(
                    RedisChannel.Literal($"room:{roomId}"),
                    new ReadOnlyMemory<byte>(buffer, 0, calculatedSize));

            // Return buffer to pool ONLY after Redis is done, but don't wait for it here
            _ = publishTask.ContinueWith(
                t => ArrayPool<byte>.Shared.Return(buffer),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return Task.CompletedTask;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
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