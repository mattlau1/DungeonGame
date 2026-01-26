using DungeonGame.Dungeon;
using DungeonServer.Application.Abstractions.Dungeon;
using Grpc.Core;

namespace DungeonServer.Service.Services.Dungeon;

public class RoomControllerService : RoomController.RoomControllerBase
{
    private readonly IDungeonController _dungeonController;

    public RoomControllerService(IDungeonController dungeonController)
    {
        _dungeonController = dungeonController;
    }

    public override Task SubscribeRoom(SubscribeRoomRequest request, IServerStreamWriter<RoomSnapshot> responseStream, ServerCallContext context)
    {
        // TODO: Implement room subscription logic with _dungeonController
        return base.SubscribeRoom(request, responseStream, context);
    }
}