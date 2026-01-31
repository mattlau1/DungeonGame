using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Movement.Contracts;

namespace DungeonServer.Application.Abstractions.Dungeon;

public interface IDungeonController
{
    Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct);

    Task<PlayerInfoResult> GetPlayerInfoAsync(int playerId, CancellationToken ct);

    Task<MovementInputResponse> SetMovementInputAsync(int playerId, float inputX, float inputY, CancellationToken ct);
}