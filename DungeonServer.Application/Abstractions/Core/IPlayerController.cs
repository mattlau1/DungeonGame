using DungeonServer.Application.Core.PlayerController.Contracts;

namespace DungeonServer.Application.Abstractions.Core;

public interface IPlayerController
{
    Task<PlayerInfoResult> SpawnPlayerAsync(SpawnPlayerRequest request, CancellationToken ct);
    Task<PlayerInfoResult> GetPlayerInfoAsync(GetPlayerInfoRequest request, CancellationToken ct);
}