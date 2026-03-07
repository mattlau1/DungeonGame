using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Rooms.Storage;

public interface IRoomSubscriptionRegistry
{
    IAsyncEnumerable<ReadOnlyMemory<byte>> SubscribeAsync(int subscriberPlayerId, int roomId, CancellationToken ct);

    Task PublishUpdateAsync(int roomId, RoomPlayerUpdate roomUpdate, CancellationToken ct);
}
