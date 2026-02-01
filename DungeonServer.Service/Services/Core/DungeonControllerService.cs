using DungeonGame.Core;
using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Service.Mappings.Core;
using DungeonServer.Service.Mappings.Shared;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Player.Contracts;
using Grpc.Core;

namespace DungeonServer.Service.Services.Core;

public class DungeonControllerService : DungeonController.DungeonControllerBase
{
    private readonly IDungeonController _dungeonController;

    public DungeonControllerService(IDungeonController dungeonController)
    {
        _dungeonController = dungeonController;
    }

    public override async Task SubscribeRoom(
        SubscribeRoomRequest request,
        IServerStreamWriter<RoomSnapshot> responseStream,
        ServerCallContext context)
    {
        IAsyncEnumerable<RoomStateSnapshot> appResponseStream =
            _dungeonController.SubscribeRoomAsync(request.PlayerId, request.RoomId, context.CancellationToken);

        await foreach (RoomStateSnapshot roomUpdate in appResponseStream)
        {
            var grpcResponse = new RoomSnapshot { RoomId = roomUpdate.RoomId };

            // TODO: Add a way to batch these into 1 call
            IEnumerable<Task<PlayerInfo>> getPlayerInfoTasks = roomUpdate.PlayerIds
                .Select(async playerId =>
                {
                    PlayerInfoResult player =
                        await _dungeonController.GetPlayerInfoAsync(playerId, context.CancellationToken);
                    return player.ToGrpcPlayerInfo();
                });

            PlayerInfo[] grpcPlayers = await Task.WhenAll(getPlayerInfoTasks);
            grpcResponse.Players.AddRange(grpcPlayers);

            await responseStream.WriteAsync(grpcResponse);
        }
    }

    public override async Task SetMovementInput(
        IAsyncStreamReader<SetMovementInputRequest> requestStream,
        IServerStreamWriter<SetMovementInputResponse> responseStream,
        ServerCallContext context)
    {
        while (await requestStream.MoveNext())
        {
            SetMovementInputRequest request = requestStream.Current;

            MovementInputResponse appResponse =
                await _dungeonController.SetMovementInputAsync(request.PlayerId, request.InputX, request.InputY,
                    context.CancellationToken);

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