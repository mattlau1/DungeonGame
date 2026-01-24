using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Dungeon.Rooms.Contracts;
using DungeonServer.Application.Dungeon.Rooms.Models;
using DungeonServer.Application.Dungeon.Rooms.Storage;

namespace DungeonServer.Application.Dungeon;

public class DungeonArchitect : IDungeonArchitect
{
    private readonly IRoomStore _roomStore;

    public DungeonArchitect(IRoomStore roomStore)
    {
        _roomStore = roomStore;
    }

    public async Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct)
    {
        RoomStateSnapshot snapshot = await _roomStore.CreateRoomAsync(GenerateNewRoom(), ct);

        return new GenerateRoomResult(snapshot);
    }

    private static RoomState GenerateNewRoom()
    {
        return new RoomState
        {
            Height = Random.Shared.Next(RoomConstants.MinRoomSize, RoomConstants.MaxRoomSize),
            Width = Random.Shared.Next(RoomConstants.MinRoomSize, RoomConstants.MaxRoomSize),
            RoomId = 0,
            RoomType = RoomType.Combat // TODO: Implement algorithm to pick this
        };
    }
}