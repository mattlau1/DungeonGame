using DungeonGame.Core;
using DungeonServer.Application.Abstractions.Core;
using Grpc.Core;

namespace DungeonServer.Service.Services.Core;

public class PlayerControllerService : PlayerController.PlayerControllerBase
{
    private IPlayerController _playerController;

    public PlayerControllerService(IPlayerController playerController)
    {
        _playerController = playerController;
    }

    public override Task<PlayerInfo> SpawnPlayer(SpawnRequest request, ServerCallContext context)
    {
        return base.SpawnPlayer(request, context);
    }

    public override Task<PlayerInfo> GetPlayerInfo(PlayerInfoRequest request, ServerCallContext context)
    {
        return base.GetPlayerInfo(request, context);
    }
}