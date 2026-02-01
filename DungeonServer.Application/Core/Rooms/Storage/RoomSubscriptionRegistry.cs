using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Rooms.Storage;

public sealed class RoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed record RoomUpdate(RoomStateSnapshot Snapshot, RoomUpdateContext Context);

    private sealed class RoomChannel
    {
        public Channel<RoomUpdate> UpdateChannel { get; } = Channel.CreateUnbounded<RoomUpdate>();
        public int SubscriberCount { get; set; }
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();

    public async IAsyncEnumerable<RoomStateSnapshot> SubscribeAsync(
        int roomId,
        int subscriberPlayerId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
        room.SubscriberCount++;

        try
        {
            await foreach (RoomUpdate update in room.UpdateChannel.Reader.ReadAllAsync(ct))
            {
                if (update.Context.ExcludePlayerId != subscriberPlayerId)
                {
                    yield return update.Snapshot;
                }
            }
        }
        finally
        {
            room.SubscriberCount--;
            if (room.SubscriberCount == 0)
            {
                _rooms.TryRemove(roomId, out _);
            }
        }
    }

    public void PublishUpdate(int roomId, RoomStateSnapshot snapshot, RoomUpdateContext context)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
        var update = new RoomUpdate(snapshot, context);

        while (!room.UpdateChannel.Writer.TryWrite(update))
        {
        }
    }
}