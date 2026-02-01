using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Rooms.Storage;

public interface IRoomSubscriptionRegistry
{
    IAsyncEnumerable<RoomStateSnapshot> SubscribeAsync(int roomId, int subscriberPlayerId, CancellationToken ct);

    void PublishUpdate(int roomId, RoomStateSnapshot snapshot, RoomUpdateContext context);
}
