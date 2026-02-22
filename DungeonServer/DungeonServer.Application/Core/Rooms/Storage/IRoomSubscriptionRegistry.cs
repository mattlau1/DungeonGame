using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Rooms.Storage;

public interface IRoomSubscriptionRegistry
{
    IAsyncEnumerable<RoomStateSnapshot> SubscribeAsync(int subscriberPlayerId, int roomId, CancellationToken ct);

    Task PublishUpdateAsync(int roomId, RoomStateSnapshot snapshot, RoomUpdateContext context, CancellationToken ct);
}
