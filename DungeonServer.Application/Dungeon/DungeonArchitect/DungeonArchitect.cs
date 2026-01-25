using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Dungeon.DungeonArchitect.Rooms.Contracts;
using DungeonServer.Application.Dungeon.DungeonArchitect.Rooms.Models;
using DungeonServer.Application.Dungeon.DungeonArchitect.Rooms.Storage;

namespace DungeonServer.Application.Dungeon.DungeonArchitect;

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
        (
            RoomType.Combat, // TODO: Implement algorithm to pick this
            Random.Shared.Next(RoomConstants.MinRoomSize, RoomConstants.MaxRoomSize),
            Random.Shared.Next(RoomConstants.MinRoomSize, RoomConstants.MaxRoomSize)
        );
    }
}