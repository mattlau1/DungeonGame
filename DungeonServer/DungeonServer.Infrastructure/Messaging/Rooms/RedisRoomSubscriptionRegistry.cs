using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DungeonGame.Core;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using Google.Protobuf;
using StackExchange.Redis;
using PlayerInfo = DungeonGame.Core.PlayerInfo;
using Location = DungeonServer.Application.Core.Shared.Location;

namespace DungeonServer.Infrastructure.Messaging.Rooms;

public sealed class RedisRoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed record RoomUpdate(RoomPlayerUpdate Update, RoomUpdateContext Context);

    private sealed class RoomChannel
    {
        public ConcurrentDictionary<Guid, (Channel<RoomUpdate> Channel, int PlayerId)> SubscriberChannels { get; } = new();

        public RoomPlayerUpdate? CurrentState { get; set; }

        public SemaphoreSlim LifecycleLock { get; } = new(1, 1);
        public bool IsSubscribedToRedis { get; set; }
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();
    private readonly IConnectionMultiplexer _redis;
    private readonly IPlayerStore _playerStore;

    public RedisRoomSubscriptionRegistry(IConnectionMultiplexer redis, IPlayerStore playerStore)
    {
        _redis = redis;
        _playerStore = playerStore;
    }

    public async IAsyncEnumerable<RoomPlayerUpdate> SubscribeAsync(
        int subscriberPlayerId,
        int roomId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var player = await _playerStore.GetPlayerAsync(subscriberPlayerId, ct);
        if (player == null)
        {
            yield break;
        }

        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());

        Guid connectionId = Guid.NewGuid();

        var subscriberChannel = Channel.CreateBounded<RoomUpdate>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        room.SubscriberChannels.TryAdd(connectionId, (subscriberChannel, subscriberPlayerId));

        if (room.CurrentState != null)
        {
            subscriberChannel.Writer.TryWrite(new RoomUpdate(room.CurrentState, RoomUpdateContext.Broadcast()));
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
            await foreach (RoomUpdate update in subscriberChannel.Reader.ReadAllAsync(ct))
            {
                if (update.Context.ExcludePlayerId != subscriberPlayerId)
                {
                    yield return update.Update;
                }
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
        RoomSnapshot? proto = RoomSnapshot.Parser.ParseFrom(value);
        RoomPlayerUpdate update = MapToPlayerUpdate(proto);

        RoomUpdateContext context = RoomUpdateContext.ExcludePlayer(proto.ExcludePlayerId);

        var roomUpdate = new RoomUpdate(update, context);

        room.CurrentState = update;

        foreach (var subscriber in room.SubscriberChannels.Values)
        {
            subscriber.Channel.Writer.TryWrite(roomUpdate);
        }
    }

    public Task PublishUpdateAsync(int roomId, RoomPlayerUpdate update, RoomUpdateContext context, CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
        room.CurrentState = update;

        var protoSnapshot = new RoomSnapshot { RoomId = roomId, ExcludePlayerId = context.ExcludePlayerId ?? 0 };

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
            Players = players,
            ExcludePlayerId = proto.ExcludePlayerId == 0 ? null : proto.ExcludePlayerId
        };
    }
}