using System.Linq;
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
        await playerStore.UpdateLocationAsync(
            snapshot.PlayerId,
            snapshot.Location,
            roomSnapshot.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot.RoomId,
            snapshot.PlayerId,
            CancellationToken.None);

        var requested = new Location(1f, 0f);

        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(snapshot.PlayerId, requested),
            CancellationToken.None);

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
        await playerStore.UpdateLocationAsync(
            snapshot.PlayerId,
            snapshot.Location,
            roomSnapshot.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot.RoomId,
            snapshot.PlayerId,
            CancellationToken.None);

        var requested = new Location(100f, 0f);

        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(snapshot.PlayerId, requested),
            CancellationToken.None);

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

        var response = await manager.SetMovementInput(
            new MovementInputRequest(9999, new Location(1, 1)),
            CancellationToken.None);

        Assert.Equal(MovementRequestStatus.InvalidPlayer, response.Status);
    }

    [Fact]
    public async Task SetMovementInput_TransitionsPlayerToEastRoom_WhenMovingThroughEastExit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.East,
            CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(11f, 5f);
        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(player.PlayerId, requested),
            CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Ok, resp.Status);
        Assert.Equal(roomSnapshot2.RoomId, resp.RoomId);
    }

    [Fact]
    public async Task SetMovementInput_TransitionsPlayerToWestRoom_WhenMovingThroughWestExit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.West,
            CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(0, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(-1f, 5f);
        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(player.PlayerId, requested),
            CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Ok, resp.Status);

        Assert.Equal(roomSnapshot2.RoomId, resp.RoomId);
    }

    [Fact]
    public async Task SetMovementInput_TransitionsPlayerToNorthRoom_WhenMovingThroughNorthExit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.North,
            CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(5, 9), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(5f, 11f);
        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(player.PlayerId, requested),
            CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Ok, resp.Status);

        Assert.Equal(roomSnapshot2.RoomId, resp.RoomId);
    }

    [Fact]
    public async Task SetMovementInput_TransitionsPlayerToSouthRoom_WhenMovingThroughSouthExit()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.South,
            CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(5, 0), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(5f, -1f);
        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(player.PlayerId, requested),
            CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Ok, resp.Status);

        Assert.Equal(roomSnapshot2.RoomId, resp.RoomId);
    }

    [Fact]
    public async Task SetMovementInput_ReturnsBlocked_WhenMovingThroughExitWithNoConnection()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(11f, 5f);
        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(player.PlayerId, requested),
            CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Blocked, resp.Status);
    }

    [Fact]
    public async Task SetMovementInput_UpdatesPlayerLocation_AfterRoomTransition()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.East,
            CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(11f, 5f);
        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(player.PlayerId, requested),
            CancellationToken.None);

        Assert.True(resp.Location.X >= 0);
        Assert.True(resp.Location.X < roomSnapshot2.Width);
        Assert.True(resp.Location.Y >= 0);
        Assert.True(resp.Location.Y < roomSnapshot2.Height);
    }

    [Fact]
    public async Task SetMovementInput_UpdatesPlayerRoomId_AfterRoomTransition()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.East,
            CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(11f, 5f);
        MovementInputResponse resp = await manager.SetMovementInput(new MovementInputRequest(player.PlayerId, requested), CancellationToken.None);

        Assert.Equal(roomSnapshot2.RoomId, resp.RoomId);
    }

    [Fact]
    public async Task SetMovementInput_RemovesPlayerFromOldRoom_AfterRoomTransition()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.East,
            CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(11f, 5f);
        await manager.SetMovementInput(new MovementInputRequest(player.PlayerId, requested), CancellationToken.None);

        RoomStateSnapshot? oldRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot1.RoomId, CancellationToken.None);
        Assert.DoesNotContain(player.PlayerId, oldRoom!.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SetMovementInput_AddsPlayerToNewRoom_AfterRoomTransition()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        await deps.RoomStore.LinkRoomsAsync(
            roomSnapshot1.RoomId,
            roomSnapshot2.RoomId,
            Direction.East,
            CancellationToken.None);

        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(11f, 5f);
        await manager.SetMovementInput(new MovementInputRequest(player.PlayerId, requested), CancellationToken.None);

        RoomStateSnapshot? newRoom = await deps.RoomStore.GetRoomAsync(roomSnapshot2.RoomId, CancellationToken.None);
        Assert.Contains(player.PlayerId, newRoom!.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SetMovementInput_MaintainsYPosition_WhenTransitioningEastWest()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

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
        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(9, startY), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(11f, startY);
        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(player.PlayerId, requested),
            CancellationToken.None);

        float expectedY = (startY / roomSnapshot1.Height) * roomSnapshot2.Height;
        Assert.Equal(expectedY, resp.Location.Y, 1);
    }

    [Fact]
    public async Task SetMovementInput_MaintainsXPosition_WhenTransitioningNorthSouth()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

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
        PlayerSnapshot player = await playerStore.CreatePlayerAsync(new Location(startX, 9), CancellationToken.None);
        await playerStore.UpdateLocationAsync(
            player.PlayerId,
            player.Location,
            roomSnapshot1.RoomId,
            CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(
            roomSnapshot1.RoomId,
            player.PlayerId,
            CancellationToken.None);

        var requested = new Location(startX, 11f);
        MovementInputResponse resp = await manager.SetMovementInput(
            new MovementInputRequest(player.PlayerId, requested),
            CancellationToken.None);

        float expectedX = (startX / roomSnapshot1.Width) * roomSnapshot2.Width;
        Assert.Equal(expectedX, resp.Location.X, 1);
    }

    [Fact]
    public async Task SetMovementInput_OldRoomBroadcasts_WhenPlayerTransitionsOut()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);
        
        await deps.RoomStore.LinkRoomsAsync(roomSnapshot1.RoomId, roomSnapshot2.RoomId, Direction.East, CancellationToken.None);
        
        PlayerSnapshot movingPlayer = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(movingPlayer.PlayerId, movingPlayer.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, movingPlayer.PlayerId, CancellationToken.None);

        PlayerSnapshot observerPlayer = await playerStore.CreatePlayerAsync(new Location(5, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(observerPlayer.PlayerId, observerPlayer.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, observerPlayer.PlayerId, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var oldRoomSnapshots = new List<RoomStateSnapshot>();

        Task subscriptionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.RoomStore.SubscribeRoomAsync(
                                   observerPlayer.PlayerId,
                                   roomSnapshot1.RoomId,
                                   cts.Token))
                {
                    oldRoomSnapshots.Add(snap);
                    if (oldRoomSnapshots.Count >= 2) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        var requested = new Location(11f, 5f);
        await manager.SetMovementInput(new MovementInputRequest(movingPlayer.PlayerId, requested), CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();
        await subscriptionTask;

        Assert.True(oldRoomSnapshots.Count >= 2);
        Assert.Contains(movingPlayer.PlayerId, oldRoomSnapshots[0].Players.Select(p => p.PlayerId));
        Assert.DoesNotContain(movingPlayer.PlayerId, oldRoomSnapshots[^1].Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SetMovementInput_NewRoomBroadcasts_WhenPlayerTransitionsIn()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);
        
        await deps.RoomStore.LinkRoomsAsync(roomSnapshot1.RoomId, roomSnapshot2.RoomId, Direction.East, CancellationToken.None);
        
        PlayerSnapshot movingPlayer = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(movingPlayer.PlayerId, movingPlayer.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, movingPlayer.PlayerId, CancellationToken.None);

        PlayerSnapshot observerPlayer = await playerStore.CreatePlayerAsync(new Location(5, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(observerPlayer.PlayerId, observerPlayer.Location, roomSnapshot2.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot2.RoomId, observerPlayer.PlayerId, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var newRoomSnapshots = new List<RoomStateSnapshot>();

        Task subscriptionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.RoomStore.SubscribeRoomAsync(
                                   observerPlayer.PlayerId,
                                   roomSnapshot2.RoomId,
                                   cts.Token))
                {
                    newRoomSnapshots.Add(snap);
                    if (newRoomSnapshots.Count >= 2) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        var requested = new Location(11f, 5f);
        await manager.SetMovementInput(new MovementInputRequest(movingPlayer.PlayerId, requested), CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();
        await subscriptionTask;

        Assert.True(newRoomSnapshots.Count >= 2);
        Assert.DoesNotContain(movingPlayer.PlayerId, newRoomSnapshots[0].Players.Select(p => p.PlayerId));
        Assert.Contains(movingPlayer.PlayerId, newRoomSnapshots[^1].Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SetMovementInput_BothRoomsBroadcast_WhenPlayerTransitions()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);
        
        await deps.RoomStore.LinkRoomsAsync(roomSnapshot1.RoomId, roomSnapshot2.RoomId, Direction.East, CancellationToken.None);
        
        PlayerSnapshot movingPlayer = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(movingPlayer.PlayerId, movingPlayer.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, movingPlayer.PlayerId, CancellationToken.None);

        PlayerSnapshot observerInOldRoom = await playerStore.CreatePlayerAsync(new Location(5, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(observerInOldRoom.PlayerId, observerInOldRoom.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, observerInOldRoom.PlayerId, CancellationToken.None);

        PlayerSnapshot observerInNewRoom = await playerStore.CreatePlayerAsync(new Location(5, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(observerInNewRoom.PlayerId, observerInNewRoom.Location, roomSnapshot2.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot2.RoomId, observerInNewRoom.PlayerId, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var oldRoomSnapshots = new List<RoomStateSnapshot>();
        var newRoomSnapshots = new List<RoomStateSnapshot>();

        Task oldRoomTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.RoomStore.SubscribeRoomAsync(
                                   observerInOldRoom.PlayerId,
                                   roomSnapshot1.RoomId,
                                   cts.Token))
                {
                    oldRoomSnapshots.Add(snap);
                    if (oldRoomSnapshots.Count >= 2) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        Task newRoomTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.RoomStore.SubscribeRoomAsync(
                                   observerInNewRoom.PlayerId,
                                   roomSnapshot2.RoomId,
                                   cts.Token))
                {
                    newRoomSnapshots.Add(snap);
                    if (newRoomSnapshots.Count >= 2) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        var requested = new Location(11f, 5f);
        await manager.SetMovementInput(new MovementInputRequest(movingPlayer.PlayerId, requested), CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();
        await Task.WhenAll(oldRoomTask, newRoomTask);

        Assert.True(oldRoomSnapshots.Count >= 2);
        Assert.True(newRoomSnapshots.Count >= 2);
        Assert.DoesNotContain(movingPlayer.PlayerId, oldRoomSnapshots[^1].Players.Select(p => p.PlayerId));
        Assert.Contains(movingPlayer.PlayerId, newRoomSnapshots[^1].Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SetMovementInput_BroadcastsToBothRooms_WhenPlayerTransitions()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);
        
        await deps.RoomStore.LinkRoomsAsync(roomSnapshot1.RoomId, roomSnapshot2.RoomId, Direction.East, CancellationToken.None);
        
        PlayerSnapshot movingPlayer = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(movingPlayer.PlayerId, movingPlayer.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, movingPlayer.PlayerId, CancellationToken.None);

        PlayerSnapshot observerPlayer = await playerStore.CreatePlayerAsync(new Location(5, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(observerPlayer.PlayerId, observerPlayer.Location, roomSnapshot2.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot2.RoomId, observerPlayer.PlayerId, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var room2Snapshots = new List<RoomStateSnapshot>();

        Task subscriptionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.RoomStore.SubscribeRoomAsync(
                                   observerPlayer.PlayerId,
                                   roomSnapshot2.RoomId,
                                   cts.Token))
                {
                    room2Snapshots.Add(snap);
                    if (room2Snapshots.Count >= 2) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);

        var requested = new Location(11f, 5f);
        await manager.SetMovementInput(new MovementInputRequest(movingPlayer.PlayerId, requested), CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();
        await subscriptionTask;

        Assert.True(room2Snapshots.Count >= 2, $"Expected at least 2 snapshots but got {room2Snapshots.Count}");
        Assert.DoesNotContain(movingPlayer.PlayerId, room2Snapshots[0].Players.Select(p => p.PlayerId));
        Assert.Contains(movingPlayer.PlayerId, room2Snapshots[^1].Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SetMovementInput_MovingPlayerReceivesNotificationOfOwnEntryIntoNewRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        var playerStore = deps.PlayerStore;
        var manager = new MovementManager(playerStore, deps.RoomStore);

        var room1 = new RoomState(RoomType.Combat, 10, 10);
        var room2 = new RoomState(RoomType.Combat, 10, 10);
        var roomSnapshot1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        var roomSnapshot2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);
        
        await deps.RoomStore.LinkRoomsAsync(roomSnapshot1.RoomId, roomSnapshot2.RoomId, Direction.East, CancellationToken.None);
        
        PlayerSnapshot movingPlayer = await playerStore.CreatePlayerAsync(new Location(9, 5), CancellationToken.None);
        await playerStore.UpdateLocationAsync(movingPlayer.PlayerId, movingPlayer.Location, roomSnapshot1.RoomId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomSnapshot1.RoomId, movingPlayer.PlayerId, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var movingPlayerSnapshots = new List<RoomStateSnapshot>();

        Task subscriptionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.RoomStore.SubscribeRoomAsync(
                                   movingPlayer.PlayerId,
                                   roomSnapshot2.RoomId,
                                   cts.Token))
                {
                    movingPlayerSnapshots.Add(snap);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);

        var requested = new Location(11f, 5f);
        await manager.SetMovementInput(new MovementInputRequest(movingPlayer.PlayerId, requested), CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();
        await subscriptionTask;

        Assert.True(movingPlayerSnapshots.Count >= 1);
    }
}
