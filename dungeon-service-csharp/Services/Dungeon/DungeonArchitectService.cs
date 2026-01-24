using DungeonGame.Dungeon;
using Grpc.Core;

namespace DungeonService.Services.Dungeon;

public class DungeonArchitectService : DungeonArchitect.DungeonArchitectBase
{
    public override Task<DungeonRoom> GenerateRoom(RoomRequest request, ServerCallContext context)
    {
        return base.GenerateRoom(request, context);
    }
}