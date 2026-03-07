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
using Location = DungeonServer.Application.Core.Shared.Location;

namespace DungeonServer.Infrastructure.Messaging.Rooms;

public sealed class RedisRoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed class RoomChannel
    {
        public ConcurrentDictionary<Guid, Channel<RoomPlayerUpdate>> SubscriberChannels { get; } = new();

        public RoomPlayerUpdate? CurrentState { get; set; }

        public SemaphoreSlim LifecycleLock { get; } = new(1, 1);
        public bool IsSubscribedToRedis { get; set; }
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();
    private readonly IConnectionMultiplexer _redis;

    public RedisRoomSubscriptionRegistry(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async IAsyncEnumerable<RoomPlayerUpdate> SubscribeAsync(
        int subscriberPlayerId,
        int roomId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());

        var connectionId = Guid.NewGuid();

        var subscriberChannel = Channel.CreateBounded<RoomPlayerUpdate>(
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
            await foreach (RoomPlayerUpdate update in subscriberChannel.Reader.ReadAllAsync(ct))
            {
                yield return update;
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
        RoomSnapshot proto = RoomSnapshot.Parser.ParseFrom(value);
        RoomPlayerUpdate update = MapToPlayerUpdate(proto);

        room.CurrentState = update;

        foreach (Channel<RoomPlayerUpdate> channel in room.SubscriberChannels.Values)
        {
            channel.Writer.TryWrite(update);
        }
    }

    public Task PublishUpdateAsync(int roomId, RoomPlayerUpdate update, CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
        room.CurrentState = update;

        var protoSnapshot = new RoomSnapshot { RoomId = roomId };

        foreach (PlayerSnapshot player in update.Players)
        {
            protoSnapshot.Players.Add(
                new PlayerInfo
                {
                    Id = player.PlayerId,
                    Location = new DungeonGame.Shared.Location { X = player.Location.X, Y = player.Location.Y }
                });
        }

        ct.ThrowIfCancellationRequested();

        byte[] data = protoSnapshot.ToByteArray();
        return _redis.GetSubscriber().PublishAsync(RedisChannel.Literal($"room:{roomId}"), data);
    }

    private static RoomPlayerUpdate MapToPlayerUpdate(RoomSnapshot proto)
    {
        List<PlayerSnapshot> players = proto.Players.Select(p => new PlayerSnapshot(
                p.Id,
                proto.RoomId,
                new Location(p.Location.X, p.Location.Y),
                true))
            .ToList();

        return new RoomPlayerUpdate
        {
            RoomId = proto.RoomId,
            Players = players
        };
    }
}