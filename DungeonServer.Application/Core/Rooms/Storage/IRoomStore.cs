using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Rooms.Storage;

public interface IRoomStore
{
    Task<RoomStateSnapshot> CreateRoomAsync(RoomState room, CancellationToken ct);

    Task<RoomStateSnapshot> UpdateRoomAsync(int roomId, Action<RoomState> updateAction, RoomUpdateContext context, CancellationToken ct);

    Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct);

    Task PublishRoomUpdateAsync(int roomId, RoomUpdateContext context, CancellationToken ct);

    IAsyncEnumerable<RoomStateSnapshot> SubscribeRoomAsync(int subscriberPlayerId, int roomId, CancellationToken ct);
}