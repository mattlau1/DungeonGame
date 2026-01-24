using DungeonGame.Application.Abstractions.Dungeon;
using DungeonGame.Application.Dungeon.GenerateRoom;
using DungeonGame.Dungeon;
using DungeonService.Mappings.Dungeon;
using Grpc.Core;

namespace DungeonService.Services.Dungeon;

public class DungeonArchitectService : DungeonArchitect.DungeonArchitectBase
{
    private readonly IDungeonArchitect _dungeonArchitect;

    public DungeonArchitectService(IDungeonArchitect dungeonArchitect)
    {
        _dungeonArchitect = dungeonArchitect;
    }
    
    public override async Task<DungeonRoom> GenerateRoom(RoomRequest request, ServerCallContext context)
    {
        var appRequest = new GenerateRoomRequest();

        var result = await _dungeonArchitect.GenerateRoomAsync(appRequest, context.CancellationToken);

        return result.ToGrpcDungeonRoom();
    }
}