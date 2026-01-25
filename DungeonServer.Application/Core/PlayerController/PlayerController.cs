using DungeonServer.Application.Abstractions.Core;
using DungeonServer.Application.Core.PlayerController.Contracts;

namespace DungeonServer.Application.Core.PlayerController;

public class PlayerController : IPlayerController
{
    public Task<PlayerInfoResult> SpawnPlayerAsync(SpawnPlayerRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<PlayerInfoResult> GetPlayerInfoAsync(GetPlayerInfoRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}