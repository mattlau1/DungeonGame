using DungeonServer.Application.Core.Player.Models;

namespace DungeonServer.Application.Core.Player.Contracts;

public interface IPlayerManager
{
    Task<PlayerInfo> SpawnPlayerAsync(CancellationToken ct);

    Task DisconnectPlayerAsync(int playerId, CancellationToken ct);
}