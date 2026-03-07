using DungeonGame.Core;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using RoomSnapshotMarshaller = DungeonServer.Service.CustomMarshallers.RoomSnapshotMarshaller;

namespace DungeonServer.Service.Services.Core;

public class DungeonControllerMethodProvider : IServiceMethodProvider<DungeonControllerService>
{
    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<DungeonControllerService> context)
    {
        context.AddServerStreamingMethod(
            DungeonControllerService.SubscribeRoomMethod,
            Array.Empty<object>(),
            (service, request, writer, ctx) => service.SubscribeRoom(request, writer, ctx));

        context.AddUnaryMethod(
            DungeonControllerService.GetRoomInfoMethod,
            Array.Empty<object>(),
            (service, request, ctx) => service.GetRoomInfo(request, ctx));

        context.AddDuplexStreamingMethod(
            DungeonControllerService.SendInputCommandMethod,
            Array.Empty<object>(),
            (service, reader, writer, ctx) => service.SendInputCommand(reader, writer, ctx));

        context.AddUnaryMethod(
            DungeonControllerService.SpawnPlayerMethod,
            Array.Empty<object>(),
            (service, request, ctx) => service.SpawnPlayer(request, ctx));

        context.AddUnaryMethod(
            DungeonControllerService.GetPlayerInfoMethod,
            Array.Empty<object>(),
            (service, request, ctx) => service.GetPlayerInfo(request, ctx));

        context.AddUnaryMethod(
            DungeonControllerService.DisconnectPlayerMethod,
            Array.Empty<object>(),
            (service, request, ctx) => service.DisconnectPlayer(request, ctx));

        context.AddUnaryMethod(
            DungeonControllerService.GetServerStatusMethod,
            Array.Empty<object>(),
            (service, request, ctx) => service.GetServerStatus(request, ctx));
    }
}