using DungeonGame.Dungeon;
using Grpc.Core;

namespace DungeonServer.Service.Services.Dungeon;

public class RoomControllerService : RoomController.RoomControllerBase
{
    public override Task SubscribeRoom(SubscribeRoomRequest request, IServerStreamWriter<RoomSnapshot> responseStream, ServerCallContext context)
    {
        return base.SubscribeRoom(request, responseStream, context);
    }
}