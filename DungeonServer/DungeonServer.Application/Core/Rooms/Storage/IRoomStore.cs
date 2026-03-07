using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Rooms.Storage;

public interface IRoomStore
{
    Task<RoomStateSnapshot> CreateRoomAsync(RoomState room, CancellationToken ct);

    Task<RoomStateSnapshot> AddPlayerToRoomAsync(int roomId, int playerId, CancellationToken ct);

    Task<RoomStateSnapshot> RemovePlayerFromRoomAsync(int roomId, int playerId, CancellationToken ct);

    Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct);

    Task PublishRoomUpdateAsync(int roomId, CancellationToken ct);

    Task LinkRoomsAsync(int roomIdA, int roomIdB, Direction directionFromAToB, CancellationToken ct);

    Task SwapRoomsAsync(int playerId, int fromRoomId, int toRoomId, CancellationToken ct);

    IAsyncEnumerable<RoomPlayerUpdate> SubscribeRoomAsync(int subscriberPlayerId, int roomId, CancellationToken ct);
}