using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using Xunit;

namespace DungeonServer.Application.Tests.Movement;

public class MovementManagerTests
{
    [Fact]
    public async Task SetMovementInput_Applies_WhenWithinSpeedLimit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);
        
        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);
        
        PlayerSnapshot snapshot = await playerStore.CreatePlayerAsync(Location.Origin, CancellationToken.None);
        await playerStore.UpdatePlayerAsync(snapshot.PlayerId, p => p.RoomId = roomSnapshot.RoomId, CancellationToken.None);
        await deps.RoomStore.UpdateRoomAsync(roomSnapshot.RoomId, r => r.PlayerIds.Add(snapshot.PlayerId), RoomUpdateContext.Broadcast(), CancellationToken.None);

        var requested = new Location(1f, 0f);  

        MovementInputResponse resp = await manager.SetMovementInput(new MovementInputRequest(snapshot.PlayerId, requested), CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Ok, resp.Status);
        Assert.Equal(requested.X, resp.Location.X);
        Assert.Equal(requested.Y, resp.Location.Y);
    }

    [Fact]
    public async Task SetMovementInput_Returns_TooFast_When_MoveExceedsLimit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);
        
        PlayerSnapshot snapshot = await playerStore.CreatePlayerAsync(Location.Origin, CancellationToken.None);
        await playerStore.UpdatePlayerAsync(snapshot.PlayerId, p => p.RoomId = roomSnapshot.RoomId, CancellationToken.None);
        await deps.RoomStore.UpdateRoomAsync(roomSnapshot.RoomId, r => r.PlayerIds.Add(snapshot.PlayerId), RoomUpdateContext.Broadcast(), CancellationToken.None);

        var requested = new Location(100f, 0f); 

        MovementInputResponse resp = await manager.SetMovementInput(new MovementInputRequest(snapshot.PlayerId, requested), CancellationToken.None);

        Assert.Equal(MovementRequestStatus.TooFast, resp.Status);
        Assert.Equal(0f, resp.Location.X);
        Assert.Equal(0f, resp.Location.Y);
    }

    [Fact]
    public async Task SetMovementInput_ReturnsInvalidPlayer_When_PlayerMissing()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var response = await manager.SetMovementInput(new MovementInputRequest(9999, new Location(1,1)), CancellationToken.None);
        
        Assert.Equal(MovementRequestStatus.InvalidPlayer, response.Status);
    }
}
