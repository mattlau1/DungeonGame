using DungeonGame.Core;
using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Abstractions.Core;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Service.Mappings.Core;
using DungeonServer.Service.Mappings.Shared;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Shared;
using Grpc.Core;

namespace DungeonServer.Service.Services.Core;

public class DungeonControllerService : DungeonController.DungeonControllerBase
{
    private readonly IDungeonController _dungeonController;
    private readonly IMovementManager _movementManager;

    public DungeonControllerService(IDungeonController dungeonController, IMovementManager movementManager)
    {
        _dungeonController = dungeonController;
        _movementManager = movementManager;
    }

    public override async Task SetMovementInput(
        IAsyncStreamReader<SetMovementInputRequest> requestStream,
        IServerStreamWriter<SetMovementInputResponse> responseStream,
        ServerCallContext context)
    {
        while (await requestStream.MoveNext())
        {
            SetMovementInputRequest req = requestStream.Current;

            PlayerInfoResult currPlayer =
                await _dungeonController.GetPlayerInfoAsync(req.PlayerId, context.CancellationToken);
            Location currLocation = currPlayer.PlayerInfo.Location;

            var destination = new Location(currLocation.X + req.InputX, currLocation.Y + req.InputY);

            var appRequest = new MovementInputRequest(req.PlayerId, destination);

            MovementInputResponse appResponse =
                await _movementManager.SetMovementInput(appRequest, context.CancellationToken);

            var grpcResponse = new SetMovementInputResponse
            {
                Result = appResponse.status switch
                {
                    MovementRequestStatus.Ok => InputResult.Ok,
                    MovementRequestStatus.Blocked => InputResult.Blocked,
                    MovementRequestStatus.TooFast => InputResult.TooFast,
                    MovementRequestStatus.InvalidPlayer => InputResult.InvalidPlayer,
                    _ => InputResult.Unspecified
                },
                AuthoritativeLocation = appResponse.location.ToGrpcLocation(),
                DebugMessage = appResponse.debugMsg
            };

            await responseStream.WriteAsync(grpcResponse);

            if (context.CancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
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