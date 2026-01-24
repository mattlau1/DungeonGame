using DungeonGame.Core;
using Grpc.Core;

namespace DungeonServer.Service.Services.Core;

public class PlayerControllerService : PlayerController.PlayerControllerBase
{
    public override Task<PlayerInfo> SpawnPlayer(SpawnRequest request, ServerCallContext context)
    {
        return base.SpawnPlayer(request, context);
    }

    public override Task<PlayerInfo> GetPlayerInfo(PlayerInfoRequest request, ServerCallContext context)
    {
        return base.GetPlayerInfo(request, context);
    }
}