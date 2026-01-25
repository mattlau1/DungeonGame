using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Rooms.Storage;

public interface IRoomStore
{
    Task<RoomStateSnapshot> CreateRoomAsync(RoomState room, CancellationToken ct);

    Task<RoomStateSnapshot> UpdateRoomAsync(int roomId, Action<RoomState> updateAction, CancellationToken ct);

    Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct);
}