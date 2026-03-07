using DungeonGame.Core;
using DungeonServer.Service.Mappings.Core;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.External;
using DungeonServer.Service.CustomMarshallers;
using Google.Protobuf;
using Grpc.Core;
using PlayerInfo = DungeonGame.Core.PlayerInfo;
using AppPlayerInfo = DungeonServer.Application.Core.Player.Models.PlayerInfo;
using MovementInput = DungeonServer.Application.Core.Movement.Models.MovementInput;

namespace DungeonServer.Service.Services.Core;

public class DungeonControllerService
{
    public static readonly Method<SubscribeRoomRequest, RoomSnapshotMarshaller.SnapshotSource> SubscribeRoomMethod =
        new(
            type: MethodType.ServerStreaming,
            serviceName: "dungeon_game.core.DungeonController",
            name: nameof(SubscribeRoom),
            requestMarshaller: Marshallers.Create(
                r => r.ToByteArray(),
                data => SubscribeRoomRequest.Parser.ParseFrom(data)),
            responseMarshaller: RoomSnapshotMarshaller.Marshaller);

    public static readonly Method<RoomInfoRequest, RoomInfo> GetRoomInfoMethod = new(
        MethodType.Unary,
        "dungeon_game.core.DungeonController",
        nameof(GetRoomInfo),
        Marshallers.Create(r => r.ToByteArray(), data => RoomInfoRequest.Parser.ParseFrom(data)),
        Marshallers.Create(r => r.ToByteArray(), data => RoomInfo.Parser.ParseFrom(data)));

    public static readonly Method<InputCommandRequest, Google.Protobuf.WellKnownTypes.Empty> SendInputCommandMethod =
        new(
            MethodType.DuplexStreaming,
            "dungeon_game.core.DungeonController",
            nameof(SendInputCommand),
            Marshallers.Create(r => r.ToByteArray(), data => InputCommandRequest.Parser.ParseFrom(data)),
            Marshallers.Create(
                _ => new Google.Protobuf.WellKnownTypes.Empty().ToByteArray(),
                data => Google.Protobuf.WellKnownTypes.Empty.Parser.ParseFrom(data)));

    public static readonly Method<SpawnRequest, PlayerInfo> SpawnPlayerMethod = new(
        MethodType.Unary,
        "dungeon_game.core.DungeonController",
        nameof(SpawnPlayer),
        Marshallers.Create(r => r.ToByteArray(), data => SpawnRequest.Parser.ParseFrom(data)),
        Marshallers.Create(r => r.ToByteArray(), data => PlayerInfo.Parser.ParseFrom(data)));

    public static readonly Method<PlayerInfoRequest, PlayerInfo> GetPlayerInfoMethod = new(
        MethodType.Unary,
        "dungeon_game.core.DungeonController",
        nameof(GetPlayerInfo),
        Marshallers.Create(r => r.ToByteArray(), data => PlayerInfoRequest.Parser.ParseFrom(data)),
        Marshallers.Create(r => r.ToByteArray(), data => PlayerInfo.Parser.ParseFrom(data)));

    public static readonly Method<DisconnectRequest, Google.Protobuf.WellKnownTypes.Empty> DisconnectPlayerMethod = new(
        MethodType.Unary,
        "dungeon_game.core.DungeonController",
        nameof(DisconnectPlayer),
        Marshallers.Create(r => r.ToByteArray(), data => DisconnectRequest.Parser.ParseFrom(data)),
        Marshallers.Create(
            _ => new Google.Protobuf.WellKnownTypes.Empty().ToByteArray(),
            data => Google.Protobuf.WellKnownTypes.Empty.Parser.ParseFrom(data)));

    public static readonly Method<Google.Protobuf.WellKnownTypes.Empty, ServerStatusResponse> GetServerStatusMethod =
        new(
            MethodType.Unary,
            "dungeon_game.core.DungeonController",
            nameof(GetServerStatus),
            Marshallers.Create(
                _ => new Google.Protobuf.WellKnownTypes.Empty().ToByteArray(),
                data => Google.Protobuf.WellKnownTypes.Empty.Parser.ParseFrom(data)),
            Marshallers.Create(r => r.ToByteArray(), data => ServerStatusResponse.Parser.ParseFrom(data)));

    private readonly IDungeonController _dungeonController;

    public DungeonControllerService(IDungeonController dungeonController)
    {
        _dungeonController = dungeonController;
    }

    public async Task<RoomInfo> GetRoomInfo(RoomInfoRequest request, ServerCallContext context)
    {
        RoomStateSnapshot? room = await _dungeonController.GetRoomAsync(request.RoomId, context.CancellationToken);
        if (room == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Room Id {request.RoomId} not found."));
        }

        return room.ToProto();
    }

    public async Task SubscribeRoom(
        SubscribeRoomRequest request,
        IServerStreamWriter<RoomSnapshotMarshaller.SnapshotSource> responseStream,
        ServerCallContext context)
    {
        try
        {
            IAsyncEnumerable<ReadOnlyMemory<byte>> appResponseStream = _dungeonController.SubscribeRoomAsync(
                request.PlayerId,
                request.RoomId,
                context.CancellationToken);
            
            await foreach (ReadOnlyMemory<byte> data in appResponseStream)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var carrier = new RoomSnapshotMarshaller.SnapshotSource();
                carrier.Update(data);

                await responseStream.WriteAsync(carrier);
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

    public async Task SendInputCommand(
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
                    Input = new MovementInput { MoveX = request.InputX, MoveY = request.InputY }
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

    public async Task<PlayerInfo> SpawnPlayer(SpawnRequest request, ServerCallContext context)
    {
        AppPlayerInfo result = await _dungeonController.SpawnPlayerAsync(context.CancellationToken);
        return result.ToProto();
    }

    public async Task<PlayerInfo> GetPlayerInfo(PlayerInfoRequest request, ServerCallContext context)
    {
        AppPlayerInfo result = await _dungeonController.GetPlayerInfoAsync(request.PlayerId, context.CancellationToken);
        return result.ToProto();
    }

    public async Task<Google.Protobuf.WellKnownTypes.Empty> DisconnectPlayer(
        DisconnectRequest request,
        ServerCallContext context)
    {
        await _dungeonController.DisconnectPlayerAsync(request.PlayerId, context.CancellationToken);
        return new Google.Protobuf.WellKnownTypes.Empty();
    }

    public async Task<ServerStatusResponse> GetServerStatus(
        Google.Protobuf.WellKnownTypes.Empty request,
        ServerCallContext context)
    {
        int count = await _dungeonController.GetActivePlayerCountAsync(context.CancellationToken);
        return new ServerStatusResponse { ActivePlayerCount = count };
    }
}