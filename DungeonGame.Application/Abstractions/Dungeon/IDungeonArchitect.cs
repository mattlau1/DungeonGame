using DungeonGame.Application.Dungeon.GenerateRoom;

namespace DungeonGame.Application.Abstractions.Dungeon;

public interface IDungeonArchitect
{
    Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct);
}