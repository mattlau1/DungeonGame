using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;
using Xunit;

namespace DungeonServer.Application.Tests.Movement;

public class MovementManagerTests
{
    [Fact]
    public async Task SimulatePhysics_AppliesMovementInput()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(1, roomSnapshot.RoomId, 0, 0);

        var command = TestHelpers.CreateInputCommand(1, 1f, 0f, 1);
        var commands = new List<InputCommand> { command };

        await deps.MovementManager.SimulatePhysics(playerState, commands, roomSnapshot, CancellationToken.None);

        Assert.Equal(1f, playerState.Position.X);
        Assert.Equal(0f, playerState.Position.Y);
    }

    [Fact]
    public async Task SimulatePhysics_SumsMultipleCommands()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(1, roomSnapshot.RoomId, 0, 0);

        var commands = new List<InputCommand>
        {
            TestHelpers.CreateInputCommand(1, 1f, 0f, 1),
            TestHelpers.CreateInputCommand(1, 1f, 0f, 2),
            TestHelpers.CreateInputCommand(1, 1f, 0f, 3)
        };

        await deps.MovementManager.SimulatePhysics(playerState, commands, roomSnapshot, CancellationToken.None);

        Assert.Equal(3f, playerState.Position.X);
        Assert.Equal(0f, playerState.Position.Y);
    }

    [Fact]
    public async Task SimulatePhysics_TransitionsPlayerToEastRoom_WhenMovingThroughEastExit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.East,
            CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, player.PlayerId, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(player.PlayerId, roomSnapshot1.RoomId, 9, 5);

        var command = TestHelpers.CreateInputCommand(player.PlayerId, 2f, 0f, 1);
        var commands = new List<InputCommand> { command };

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot1.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        Assert.Equal(roomSnapshot2.RoomId, playerState.RoomId);
    }

    [Fact]
    public async Task SimulatePhysics_TransitionsPlayerToWestRoom_WhenMovingThroughWestExit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.West,
            CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(0, 5), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, player.PlayerId, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(player.PlayerId, roomSnapshot1.RoomId, 0, 5);

        var command = TestHelpers.CreateInputCommand(player.PlayerId, -1f, 0f, 1);
        var commands = new List<InputCommand> { command };

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot1.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        Assert.Equal(roomSnapshot2.RoomId, playerState.RoomId);
    }

    [Fact]
    public async Task SimulatePhysics_TransitionsPlayerToNorthRoom_WhenMovingThroughNorthExit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.North,
            CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(5, 9), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, player.PlayerId, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(player.PlayerId, roomSnapshot1.RoomId, 5, 9);

        var command = TestHelpers.CreateInputCommand(player.PlayerId, 0f, 2f, 1);
        var commands = new List<InputCommand> { command };

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot1.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        Assert.Equal(roomSnapshot2.RoomId, playerState.RoomId);
    }

    [Fact]
    public async Task SimulatePhysics_TransitionsPlayerToSouthRoom_WhenMovingThroughSouthExit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.South,
            CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(5, 0), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, player.PlayerId, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(player.PlayerId, roomSnapshot1.RoomId, 5, 0);

        var command = TestHelpers.CreateInputCommand(player.PlayerId, 0f, -1f, 1);
        var commands = new List<InputCommand> { command };

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot1.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        Assert.Equal(roomSnapshot2.RoomId, playerState.RoomId);
    }

    [Fact]
    public async Task SimulatePhysics_PlayerStaysInRoom_WhenNoExitConnection()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot.RoomId, player.PlayerId, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(player.PlayerId, roomSnapshot.RoomId, 9, 5);

        var command = TestHelpers.CreateInputCommand(player.PlayerId, 2f, 0f, 1);
        var commands = new List<InputCommand> { command };

        await deps.MovementManager.SimulatePhysics(playerState, commands, roomSnapshot, CancellationToken.None);

        Assert.Equal(roomSnapshot.RoomId, playerState.RoomId);
    }

    [Fact]
    public async Task SimulatePhysics_UpdatesPlayerRoomId_AfterRoomTransition()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.East,
            CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, player.PlayerId, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(player.PlayerId, roomSnapshot1.RoomId, 9, 5);

        var command = TestHelpers.CreateInputCommand(player.PlayerId, 2f, 0f, 1);
        var commands = new List<InputCommand> { command };

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot1.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        Assert.Equal(roomSnapshot2.RoomId, playerState.RoomId);
    }

    [Fact]
    public async Task SimulatePhysics_MaintainsYPosition_WhenTransitioningEastWest()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.East,
            CancellationToken.None);

        float startY = 3.5f;
        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(9, startY), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, player.PlayerId, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(player.PlayerId, roomSnapshot1.RoomId, 9, startY);

        var command = TestHelpers.CreateInputCommand(player.PlayerId, 2f, 0f, 1);
        var commands = new List<InputCommand> { command };

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot1.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        float expectedY = (startY / roomSnapshot1.Height) * roomSnapshot2.Height;
        Assert.Equal(expectedY, playerState.Position.Y, 1);
    }

    [Fact]
    public async Task SimulatePhysics_MaintainsXPosition_WhenTransitioningNorthSouth()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.North,
            CancellationToken.None);

        float startX = 4.5f;
        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(startX, 9), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, player.PlayerId, CancellationToken.None);

        var playerState = TestHelpers.CreatePlayerState(player.PlayerId, roomSnapshot1.RoomId, startX, 9);

        var command = TestHelpers.CreateInputCommand(player.PlayerId, 0f, 2f, 1);
        var commands = new List<InputCommand> { command };

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot1.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        float expectedX = (startX / roomSnapshot1.Width) * roomSnapshot2.Width;
        Assert.Equal(expectedX, playerState.Position.X, 1);
    }
}