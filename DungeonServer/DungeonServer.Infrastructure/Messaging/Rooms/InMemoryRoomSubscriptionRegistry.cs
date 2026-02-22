using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;

namespace DungeonServer.Infrastructure.Messaging.Rooms;

public sealed class InMemoryRoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed record RoomUpdate(RoomPlayerUpdate Update, RoomUpdateContext Context);

    private sealed class RoomChannel
    {
        public ConcurrentDictionary<Guid, (Channel<RoomUpdate> Channel, int PlayerId)> SubscriberChannels { get; } = new();

        public RoomPlayerUpdate? CurrentState { get; set; }

        public int SubscriberCount;
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();
    private readonly IPlayerStore _playerStore;

    public InMemoryRoomSubscriptionRegistry(IPlayerStore playerStore)
    {
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
        Interlocked.Increment(ref room.SubscriberCount);

        if (room.CurrentState != null)
        {
            subscriberChannel.Writer.TryWrite(new RoomUpdate(room.CurrentState, RoomUpdateContext.Broadcast()));
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
            if (Interlocked.Decrement(ref room.SubscriberCount) == 0)
            {
                _rooms.TryRemove(roomId, out _);
            }
        }
    }

    public Task PublishUpdateAsync(int roomId, RoomPlayerUpdate update, RoomUpdateContext context, CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
        room.CurrentState = update;

        if (room.SubscriberCount <= 0)
        {
            return Task.CompletedTask;
        }

        var roomUpdate = new RoomUpdate(update, context);

        foreach (var subscriber in room.SubscriberChannels.Values)
        {
            subscriber.Channel.Writer.TryWrite(roomUpdate);
        }

        return Task.CompletedTask;
    }
}