using DungeonServer.Application.Dungeon.Rooms.Contracts;

namespace DungeonServer.Application.Abstractions.Dungeon;

public interface IDungeonArchitect
{
    Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct);
}