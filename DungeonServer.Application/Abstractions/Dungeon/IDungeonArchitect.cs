using DungeonServer.Application.Dungeon.GenerateRoom;

namespace DungeonServer.Application.Abstractions.Dungeon;

public interface IDungeonArchitect
{
    Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct);
}