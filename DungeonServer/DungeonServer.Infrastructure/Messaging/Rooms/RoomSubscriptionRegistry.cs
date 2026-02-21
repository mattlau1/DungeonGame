using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DungeonServer.Infrastructure.Messaging.Rooms;

public sealed class RoomSubscriptionRegistry : IRoomSubscriptionRegistry
{
    private sealed record RoomUpdate(RoomStateSnapshot Snapshot, RoomUpdateContext Context);

    private sealed class RoomChannel
    {
        public Channel<RoomUpdate> UpdateChannel { get; } = Channel.CreateBounded<RoomUpdate>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        public RoomStateSnapshot? CurrentState { get; set; }

        public int SubscriberCount { get; set; }
    }

    private readonly ConcurrentDictionary<int, RoomChannel> _rooms = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public RoomSubscriptionRegistry(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async IAsyncEnumerable<RoomStateSnapshot> SubscribeAsync(
        int subscriberPlayerId,
        int roomId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IPlayerStore playerStore = scope.ServiceProvider.GetRequiredService<IPlayerStore>();
        
        PlayerSnapshot? player = await playerStore.GetPlayerAsync(subscriberPlayerId, ct);
        if (player == null)
        {
            yield break;
        }

        RoomChannel room = _rooms.GetOrAdd(roomId, _ => new RoomChannel());
        room.SubscriberCount++;

        try
        {
            // Emit the current state immediately on subscribe
            if (room.CurrentState != null)
            {
                yield return room.CurrentState;
            }
            
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
        room.CurrentState = snapshot;

        if (room.SubscriberCount > 0)
        {
            var update = new RoomUpdate(snapshot, context);

            while (!room.UpdateChannel.Writer.TryWrite(update))
            {
            }
        }
    }
}