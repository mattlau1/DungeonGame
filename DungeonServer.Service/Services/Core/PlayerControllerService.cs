using DungeonGame.Core;
using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Core.PlayerController.Contracts;
using DungeonServer.Service.Mappings.Core;
using Grpc.Core;

namespace DungeonServer.Service.Services.Core;

public class PlayerControllerService : PlayerController.PlayerControllerBase
{
    private readonly IDungeonController _dungeonController;

    public PlayerControllerService(IDungeonController dungeonController)
    {
        _dungeonController = dungeonController;
    }

    public override async Task<PlayerInfo> SpawnPlayer(SpawnRequest request, ServerCallContext context)
    {
        PlayerInfoResult result = await _dungeonController.SpawnPlayerAsync(context.CancellationToken);
        return result.ToGrpcPlayerInfo();
    }

    public override async Task<PlayerInfo> GetPlayerInfo(PlayerInfoRequest request, ServerCallContext context)
    {
        PlayerInfoResult result = await _dungeonController.GetPlayerInfoAsync(request.PlayerId, context.CancellationToken);
        return result.ToGrpcPlayerInfo();
    }
}