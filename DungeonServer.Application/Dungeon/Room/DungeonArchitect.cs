using DungeonServer.Application.Abstractions.Dungeon;

namespace DungeonServer.Application.Dungeon.Room;

public class DungeonArchitect : IDungeonArchitect
{
    private readonly IRoomStore _roomStore;

    private readonly Random _numberGenerator = new();

    public DungeonArchitect(IRoomStore roomStore)
    {
        _roomStore = roomStore;
    }

    public async Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct)
    {
        RoomStateSnapshot res = await _roomStore.CreateRoomAsync(GenerateNewRoom(), ct);

        return new GenerateRoomResult(res);
    }

    private RoomState GenerateNewRoom()
    {
        return new RoomState
        {
            Height = _numberGenerator.Next(RoomConstants.MinRoomSize, RoomConstants.MaxRoomSize),
            Width = _numberGenerator.Next(RoomConstants.MinRoomSize, RoomConstants.MaxRoomSize),
            RoomId = 0,
            RoomType = RoomType.Combat // TODO: Implement algorithm to pick this
        };
    }
}