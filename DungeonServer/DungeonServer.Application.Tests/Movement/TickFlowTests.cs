using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;
using Xunit;

namespace DungeonServer.Application.Tests.Movement;

public class TickFlowTests
{
    [Fact]
    public async Task QueuedCommands_ProcessedOnTick()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(Location.Origin, CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot.RoomId, player.PlayerId, CancellationToken.None);
        
        deps.PlayerStateManager.AddPlayerToRoom(player.PlayerId, roomSnapshot.RoomId, player.Location);

        deps.PlayerInputManager.EnqueueCommand(TestHelpers.CreateInputCommand(player.PlayerId, 1f, 0f, 1));

        var playerState = deps.PlayerStateManager.GetPlayerState(player.PlayerId);
        Assert.NotNull(playerState);

        var commands = deps.PlayerInputManager.DequeueAllForPlayer(player.PlayerId);
        Assert.Single(commands);

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        Assert.Equal(1f, playerState.Position.X);
    }

    [Fact]
    public async Task MultipleQueuedCommands_ProcessedInOrder()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(Location.Origin, CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot.RoomId, player.PlayerId, CancellationToken.None);
        
        deps.PlayerStateManager.AddPlayerToRoom(player.PlayerId, roomSnapshot.RoomId, player.Location);

        deps.PlayerInputManager.EnqueueCommand(TestHelpers.CreateInputCommand(player.PlayerId, 1f, 0f, 1));
        deps.PlayerInputManager.EnqueueCommand(TestHelpers.CreateInputCommand(player.PlayerId, 1f, 0f, 2));
        deps.PlayerInputManager.EnqueueCommand(TestHelpers.CreateInputCommand(player.PlayerId, 1f, 0f, 3));

        var playerState = deps.PlayerStateManager.GetPlayerState(player.PlayerId);
        Assert.NotNull(playerState);

        var commands = deps.PlayerInputManager.DequeueAllForPlayer(player.PlayerId);
        Assert.Equal(3, commands.Count);
        Assert.Equal(1u, commands[0].Sequence);
        Assert.Equal(2u, commands[1].Sequence);
        Assert.Equal(3u, commands[2].Sequence);

        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        Assert.Equal(3f, playerState.Position.X);
    }

    [Fact]
    public async Task PlayerStateManager_TracksPlayerAfterTick()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(5, 5), CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot.RoomId, player.PlayerId, CancellationToken.None);
        
        deps.PlayerStateManager.AddPlayerToRoom(player.PlayerId, roomSnapshot.RoomId, player.Location);

        deps.PlayerInputManager.EnqueueCommand(TestHelpers.CreateInputCommand(player.PlayerId, 2f, 0f, 1));

        var playerState = deps.PlayerStateManager.GetPlayerState(player.PlayerId);
        Assert.NotNull(playerState);

        var commands = deps.PlayerInputManager.DequeueAllForPlayer(player.PlayerId);
        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);

        var updatedState = deps.PlayerStateManager.GetPlayerState(player.PlayerId);
        Assert.NotNull(updatedState);
        Assert.Equal(7f, updatedState.Position.X);
        Assert.Equal(5f, updatedState.Position.Y);
    }

    [Fact]
    public async Task DequeueAll_ClearsQueue()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(Location.Origin, CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot.RoomId, player.PlayerId, CancellationToken.None);
        
        deps.PlayerStateManager.AddPlayerToRoom(player.PlayerId, roomSnapshot.RoomId, player.Location);

        deps.PlayerInputManager.EnqueueCommand(TestHelpers.CreateInputCommand(player.PlayerId, 1f, 0f, 1));

        var commands1 = deps.PlayerInputManager.DequeueAllForPlayer(player.PlayerId);
        Assert.Single(commands1);

        var commands2 = deps.PlayerInputManager.DequeueAllForPlayer(player.PlayerId);
        Assert.Empty(commands2);
    }

    [Fact]
    public async Task LastProcessedSequence_UpdatedAfterTick()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 20, 20);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var player = await deps.PlayerStore.CreatePlayerAsync(Location.Origin, CancellationToken.None);
        await deps.PlayerStore.UpdateLocationAsync(player.PlayerId, player.Location, roomSnapshot.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot.RoomId, player.PlayerId, CancellationToken.None);
        
        deps.PlayerStateManager.AddPlayerToRoom(player.PlayerId, roomSnapshot.RoomId, player.Location);

        deps.PlayerInputManager.EnqueueCommand(TestHelpers.CreateInputCommand(player.PlayerId, 1f, 0f, 5));
        deps.PlayerInputManager.EnqueueCommand(TestHelpers.CreateInputCommand(player.PlayerId, 1f, 0f, 10));

        var playerState = deps.PlayerStateManager.GetPlayerState(player.PlayerId);
        Assert.NotNull(playerState);
        Assert.Equal(0u, playerState.LastProcessedSequence);

        var commands = deps.PlayerInputManager.DequeueAllForPlayer(player.PlayerId);
        var currentRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot.RoomId, CancellationToken.None);
        await deps.MovementManager.SimulatePhysics(playerState, commands, currentRoom, CancellationToken.None);
        playerState.LastProcessedSequence = commands[^1].Sequence;

        Assert.Equal(10u, playerState.LastProcessedSequence);
    }
}
