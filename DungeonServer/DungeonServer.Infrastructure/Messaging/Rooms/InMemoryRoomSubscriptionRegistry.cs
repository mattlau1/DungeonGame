using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;

namespace DungeonServer.Infrastructure.Messaging.Rooms;

public sealed class InMemoryRoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed class RoomChannel
    {
        public ConcurrentDictionary<Guid, Channel<RoomPlayerUpdate>> SubscriberChannels { get; } = new();

        public RoomPlayerUpdate? CurrentState { get; set; }
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();

    public InMemoryRoomSubscriptionRegistry()
    {
    }

    public async IAsyncEnumerable<RoomPlayerUpdate> SubscribeAsync(
        int subscriberPlayerId,
        int roomId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());

        Guid connectionId = Guid.NewGuid();

        var subscriberChannel = Channel.CreateBounded<RoomPlayerUpdate>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        room.SubscriberChannels.TryAdd(connectionId, subscriberChannel);

        if (room.CurrentState != null)
        {
            subscriberChannel.Writer.TryWrite(room.CurrentState);
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
            if (room.SubscriberChannels.IsEmpty)
            {
                _rooms.TryRemove(roomId, out _);
            }
        }
    }

    public Task PublishUpdateAsync(int roomId, RoomPlayerUpdate update, CancellationToken ct)
    {
        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
        room.CurrentState = update;

        if (room.SubscriberChannels.IsEmpty)
        {
            return Task.CompletedTask;
        }

        foreach (Channel<RoomPlayerUpdate> channel in room.SubscriberChannels.Values)
        {
            channel.Writer.TryWrite(update);
        }

        return Task.CompletedTask;
    }
}
