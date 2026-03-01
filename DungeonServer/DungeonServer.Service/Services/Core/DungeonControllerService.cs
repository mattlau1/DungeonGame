using DungeonGame.Core;
using DungeonServer.Service.Mappings.Core;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.External;
using Grpc.Core;
using PlayerInfo = DungeonGame.Core.PlayerInfo;
using AppPlayerInfo = DungeonServer.Application.Core.Player.Models.PlayerInfo;
using MovementInput = DungeonServer.Application.Core.Movement.Models.MovementInput;

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
            IAsyncEnumerable<RoomPlayerUpdate> appResponseStream = _dungeonController.SubscribeRoomAsync(
                request.PlayerId,
                request.RoomId,
                context.CancellationToken);

            await foreach (RoomPlayerUpdate roomUpdate in appResponseStream)
            {
                var grpcResponse = new RoomSnapshot { RoomId = roomUpdate.RoomId };

                foreach (PlayerSnapshot player in roomUpdate.Players)
                {
                    grpcResponse.Players.Add(player.ToProto());
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

    public override async Task SendInputCommand(
        IAsyncStreamReader<InputCommandRequest> requestStream,
        IServerStreamWriter<Google.Protobuf.WellKnownTypes.Empty> responseStream,
        ServerCallContext context)
    {
        int? lastPlayerId = null;
        try
        {
            while (await requestStream.MoveNext())
            {
                InputCommandRequest request = requestStream.Current;
                lastPlayerId = request.PlayerId;

                var inputCommand = new InputCommand
                {
                    PlayerId = request.PlayerId,
                    Sequence = request.Sequence,
                    ClientTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Input = new MovementInput
                    {
                        MoveX = request.InputX,
                        MoveY = request.InputY
                    }
                };

                await _dungeonController.SendInputCommandAsync(inputCommand, context.CancellationToken);

                if (context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
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
        AppPlayerInfo result = await _dungeonController.SpawnPlayerAsync(context.CancellationToken);
        return result.ToProto();
    }

    public override async Task<PlayerInfo> GetPlayerInfo(PlayerInfoRequest request, ServerCallContext context)
    {
        AppPlayerInfo result = await _dungeonController.GetPlayerInfoAsync(request.PlayerId, context.CancellationToken);
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