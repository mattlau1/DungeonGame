using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;

namespace DungeonServer.Infrastructure.Messaging.Rooms;

public sealed class RoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed record RoomUpdate(RoomPlayerUpdate Update, RoomUpdateContext Context);

    private sealed class RoomChannel
    {
        public Channel<RoomUpdate> UpdateChannel { get; } = Channel.CreateBounded<RoomUpdate>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        public RoomPlayerUpdate? CurrentState { get; set; }

        public int SubscriberCount;
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();
    private readonly IPlayerStore _playerStore;

    public RoomSubscriptionRegistry(IPlayerStore playerStore)
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
        Interlocked.Increment(ref room.SubscriberCount);

        try
        {
            if (room.CurrentState != null)
            {
                yield return room.CurrentState;
            }

            await foreach (RoomUpdate update in room.UpdateChannel.Reader.ReadAllAsync(ct))
            {
                if (update.Context.ExcludePlayerId != subscriberPlayerId)
                {
                    yield return update.Update;
                }
            }
        }
        finally
        {
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

        while (!room.UpdateChannel.Writer.TryWrite(roomUpdate))
        {
        }

        return Task.CompletedTask;
    }
}