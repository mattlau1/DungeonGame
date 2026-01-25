using DungeonServer.Application.Core.Player.Contracts;

namespace DungeonServer.Application.Abstractions.Dungeon;

public interface IDungeonController
{
    Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct);

    Task<PlayerInfoResult> GetPlayerInfoAsync(int playerId, CancellationToken ct);
}