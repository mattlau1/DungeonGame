using Grpc.Core;
using DungeonGame.Protocol;

namespace DungeonService.Services;

public class DungeonArchitectService(ILogger<DungeonArchitectService> logger) : DungeonArchitect.DungeonArchitectBase
{
    public override Task<DungeonRoom> GenerateRoom(RoomRequest request, ServerCallContext context)
    {
        logger.LogInformation("Generating room for Seed: {Seed}, Floor: {Floor}", request.Seed, request.FloorLevel);

        // todo: add procedural generation here
        var room = new DungeonRoom
        {
            RoomId = 1,
            RoomType = "Treasure",
            Width = 13,
            Height = 13
        };

        room.SpawnableItems.Add(new Item { ItemId = 1, Name = "Stick" });

        room.InitialEnemies.Add(new Enemy { Type = "Wolf", X = 6.5f, Y = 6.5f, Health = 10 });

        return Task.FromResult(room);
    }
}