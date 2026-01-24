using DungeonGame.Application.Abstractions;
using DungeonGame.Application.Abstractions.Dungeon;
using DungeonGame.Application.Dungeon.GenerateRoom;

namespace DungeonGame.Application.Dungeon;

public class DungeonArchitect : IDungeonArchitect
{
    public Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}