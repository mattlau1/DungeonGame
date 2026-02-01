using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Player.Contracts;

namespace DungeonServer.Application.Abstractions.Dungeon;

public interface IDungeonController
{
    Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct);

    Task<PlayerInfoResult> GetPlayerInfoAsync(int playerId, CancellationToken ct);

    Task<MovementInputResponse> SetMovementInputAsync(int playerId, float inputX, float inputY, CancellationToken ct);

    IAsyncEnumerable<RoomStateSnapshot> SubscribeRoomAsync(int playerId, int roomId, CancellationToken ct);
}