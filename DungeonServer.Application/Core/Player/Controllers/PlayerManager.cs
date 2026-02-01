using DungeonServer.Application.Core.Player.Contracts;

namespace DungeonServer.Application.Core.Player.Controllers;

public class PlayerManager : IPlayerManager
{
    public Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}