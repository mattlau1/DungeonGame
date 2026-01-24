using DungeonGame.Dungeon;
using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Dungeon.Rooms.Contracts;
using DungeonServer.Service.Mappings.Dungeon;
using Grpc.Core;
using DungeonArchitect = DungeonGame.Dungeon.DungeonArchitect;

namespace DungeonServer.Service.Services.Dungeon;

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