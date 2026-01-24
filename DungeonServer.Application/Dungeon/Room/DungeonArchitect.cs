using DungeonServer.Application.Abstractions.Dungeon;

namespace DungeonServer.Application.Dungeon.Room;

public class DungeonArchitect : IDungeonArchitect
{
    private IRoomStore _roomStore;

    public DungeonArchitect(IRoomStore roomStore)
    {
        _roomStore = roomStore;
    }

    public Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}