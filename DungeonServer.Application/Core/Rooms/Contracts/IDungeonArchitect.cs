namespace DungeonServer.Application.Core.Rooms.Contracts;

public interface IDungeonArchitect
{
    Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct);
}