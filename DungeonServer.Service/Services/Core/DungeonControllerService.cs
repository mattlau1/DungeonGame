using DungeonGame.Core;
using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Service.Mappings.Core;
using Grpc.Core;

namespace DungeonServer.Service.Services.Core;

public class DungeonControllerService : DungeonController.DungeonControllerBase
{
    private readonly IDungeonController _dungeonController;
    public DungeonControllerService(IDungeonController dungeonController)
    {
        _dungeonController = dungeonController;
    }

    public override Task<SetMovementInputResponse> SetMovementInput(
        SetMovementInputRequest request,
        ServerCallContext context)
    {
        return base.SetMovementInput(request, context);
    }

    public override async Task<PlayerInfo> SpawnPlayer(SpawnRequest request, ServerCallContext context)
    {
        PlayerInfoResult result = await _dungeonController.SpawnPlayerAsync(context.CancellationToken);
        return result.ToGrpcPlayerInfo();
    }

    public override async Task<PlayerInfo> GetPlayerInfo(PlayerInfoRequest request, ServerCallContext context)
    {
        PlayerInfoResult result =
            await _dungeonController.GetPlayerInfoAsync(request.PlayerId, context.CancellationToken);
        return result.ToGrpcPlayerInfo();
    }
}