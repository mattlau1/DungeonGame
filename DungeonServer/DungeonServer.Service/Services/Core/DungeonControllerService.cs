using DungeonGame.Core;
using DungeonServer.Service.Mappings.Core;
using DungeonServer.Service.Mappings.Shared;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.External;
using Grpc.Core;

namespace DungeonServer.Service.Services.Core;

public class DungeonControllerService : DungeonController.DungeonControllerBase
{
    private readonly IDungeonController _dungeonController;

    public DungeonControllerService(IDungeonController dungeonController)
    {
        _dungeonController = dungeonController;
    }

    public override async Task<RoomInfo> GetRoomInfo(RoomInfoRequest request, ServerCallContext context)
    {
        RoomStateSnapshot? room = await _dungeonController.GetRoomAsync(request.RoomId, context.CancellationToken);
        if (room == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Room Id {request.RoomId} not found."));
        }

        return room.ToProto();
    }

    public override async Task SubscribeRoom(
        SubscribeRoomRequest request,
        IServerStreamWriter<RoomSnapshot> responseStream,
        ServerCallContext context)
    {
        try
        {
            IAsyncEnumerable<RoomStateSnapshot> appResponseStream = _dungeonController.SubscribeRoomAsync(
                request.PlayerId,
                request.RoomId,
                context.CancellationToken);

            await foreach (RoomStateSnapshot roomUpdate in appResponseStream)
            {
                var grpcResponse = new RoomSnapshot { RoomId = roomUpdate.RoomId };

                foreach (PlayerSnapshot player in roomUpdate.Players)
                {
                    try
                    {
                        var result = await _dungeonController.GetPlayerInfoAsync(
                            player.PlayerId,
                            context.CancellationToken);
                        grpcResponse.Players.Add(result.ToProto());
                    }
                    catch (KeyNotFoundException)
                    {
                        // Player disconnected since the update was queued; skip them.
                    }
                }

                await responseStream.WriteAsync(grpcResponse);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
            // Client reset the stream abruptly (Common in benchmarking under load)
        }
        finally
        {
            try
            {
                await _dungeonController.DisconnectPlayerAsync(request.PlayerId, CancellationToken.None);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    public override async Task SetMovementInput(
        IAsyncStreamReader<SetMovementInputRequest> requestStream,
        IServerStreamWriter<SetMovementInputResponse> responseStream,
        ServerCallContext context)
    {
        int? lastPlayerId = null;
        try
        {
            while (await requestStream.MoveNext())
            {
                SetMovementInputRequest request = requestStream.Current;
                lastPlayerId = request.PlayerId;

                MovementInputResponse appResponse = await _dungeonController.SetMovementInputAsync(
                    request.PlayerId,
                    request.InputX,
                    request.InputY,
                    context.CancellationToken);

                SetMovementInputResponse grpcResponse = appResponse.ToProto();

                await responseStream.WriteAsync(grpcResponse);

                if (context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal disconnection
        }
        catch (IOException)
        {
            // Client reset the stream abruptly
        }
        finally
        {
            if (lastPlayerId.HasValue)
            {
                try
                {
                    await _dungeonController.DisconnectPlayerAsync(lastPlayerId.Value, CancellationToken.None);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }

    public override async Task<PlayerInfo> SpawnPlayer(SpawnRequest request, ServerCallContext context)
    {
        PlayerInfoResult result = await _dungeonController.SpawnPlayerAsync(context.CancellationToken);
        return result.ToProto();
    }

    public override async Task<PlayerInfo> GetPlayerInfo(PlayerInfoRequest request, ServerCallContext context)
    {
        PlayerInfoResult result =
            await _dungeonController.GetPlayerInfoAsync(request.PlayerId, context.CancellationToken);
        return result.ToProto();
    }

    public override async Task<Google.Protobuf.WellKnownTypes.Empty> DisconnectPlayer(
        DisconnectRequest request,
        ServerCallContext context)
    {
        await _dungeonController.DisconnectPlayerAsync(request.PlayerId, context.CancellationToken);
        return new Google.Protobuf.WellKnownTypes.Empty();
    }

    public override async Task<ServerStatusResponse> GetServerStatus(
        Google.Protobuf.WellKnownTypes.Empty request,
        ServerCallContext context)
    {
        int count = await _dungeonController.GetActivePlayerCountAsync(context.CancellationToken);
        return new ServerStatusResponse { ActivePlayerCount = count };
    }
}