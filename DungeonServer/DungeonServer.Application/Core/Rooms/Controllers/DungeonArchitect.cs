using DungeonServer.Application.Core.Rooms.Contracts;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.TickSystem.Contracts;

namespace DungeonServer.Application.Core.Rooms.Controllers;

public class DungeonArchitect : IDungeonArchitect
{
    private readonly IRoomStore _roomStore;

    private readonly ITickScheduler _tickRunner;

    public DungeonArchitect(IRoomStore roomStore, ITickScheduler tickRunner)
    {
        _roomStore = roomStore;
        _tickRunner = tickRunner;
    }

    public async Task<GenerateRoomResult> GenerateRoomAsync(GenerateRoomRequest request, CancellationToken ct)
    {
        RoomStateSnapshot snapshot = await _roomStore.CreateRoomAsync(GenerateNewRoom(), ct);
        
        _tickRunner.RegisterRoom(snapshot.RoomId);
        
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