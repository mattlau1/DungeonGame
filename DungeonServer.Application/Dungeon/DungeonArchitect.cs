using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Dungeon.GenerateRoom;

namespace DungeonServer.Application.Dungeon;

public class DungeonArchitect : IDungeonArchitect
{
    public Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}