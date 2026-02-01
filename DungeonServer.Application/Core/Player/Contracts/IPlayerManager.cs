namespace DungeonServer.Application.Core.Player.Contracts;

public interface IPlayerManager
{
    Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct);
}