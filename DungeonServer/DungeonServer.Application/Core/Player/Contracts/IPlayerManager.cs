namespace DungeonServer.Application.Core.Player.Contracts;

public interface IPlayerManager
{
    Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct);

    Task DisconnectPlayerAsync(int playerId, CancellationToken ct);
}