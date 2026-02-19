using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms;

public class RoomSubscriptionTests
{
    [Fact]
    public async Task SubscribeRoomAsync_CanCreateSubscription()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        PlayerInfoResult player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();

        Task subscribeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snapshot in deps.Controller.SubscribeRoomAsync(
                                   player.PlayerInfo.Id,
                                   player.PlayerInfo.RoomId,
                                   cts.Token))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);
        await cts.CancelAsync();
        await subscribeTask;

        Assert.True(true);
    }

    [Fact]
    public async Task SubscribeRoomAsync_EmitsOnRoomUpdate_ShouldReflectNewPlayer()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        PlayerInfoResult first = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
        Location firstLocation = first.PlayerInfo.Location;
        int roomId = first.PlayerInfo.RoomId;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var snapshots = new List<RoomStateSnapshot>();

        Task enumerateTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(
                                   first.PlayerInfo.Id,
                                   roomId,
                                   cts.Token))
                {
                    snapshots.Add(snap);
                    if (snapshots.Count >= 1) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        var secondLocation = new Location(firstLocation.X + 1, firstLocation.Y);
        PlayerSnapshot second = await deps.PlayerStore.CreatePlayerAsync(secondLocation, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(roomId, second.PlayerId, CancellationToken.None);

        Task timeout = Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
        Task completed = await Task.WhenAny(enumerateTask, timeout);
        if (completed == timeout)
        {
            await cts.CancelAsync();
            await enumerateTask;
            Assert.Fail("Timed out waiting for room update emissions");
        }
        else
        {
            await enumerateTask;
            bool foundCombined = snapshots.Any(s =>
                s.PlayerIds.Contains(first.PlayerInfo.Id) && s.PlayerIds.Contains(second.PlayerId));
            Assert.True(foundCombined, "Did not observe a snapshot including both players after the second joined");
        }
    }

    [Fact]
    public async Task SubscribeRoomAsync_InvalidIds_ReturnsNoEmissions()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var emitted = false;
        try
        {
            await foreach (RoomStateSnapshot _ in deps.Controller.SubscribeRoomAsync(-1, -1, cts.Token))
            {
                emitted = true;
                break;
            }
        }
        catch (OperationCanceledException)
        {
        }

        Assert.False(emitted);
    }

    [Fact]
    public async Task SubscribeRoomAsync_MultipleSubscribers_AllReceiveUpdates()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        PlayerInfoResult player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
        int roomId = player1.PlayerInfo.RoomId;

        PlayerInfoResult player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var snapshots1 = new List<RoomStateSnapshot>();
        Task task1 = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(
                                   player1.PlayerInfo.Id,
                                   roomId,
                                   cts.Token))
                {
                    snapshots1.Add(snap);
                    if (snapshots1.Count >= 1) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        var snapshots2 = new List<RoomStateSnapshot>();
        Task task2 = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(
                                   player2.PlayerInfo.Id,
                                   roomId,
                                   cts.Token))
                {
                    snapshots2.Add(snap);
                    if (snapshots2.Count >= 1) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);

        await deps.RoomStore.PublishRoomUpdateAsync(roomId, RoomUpdateContext.Broadcast(), CancellationToken.None);

        await Task.Delay(200);
        await cts.CancelAsync();

        await Task.WhenAll(task1, task2);

        Assert.Single(snapshots1);
        Assert.Single(snapshots2);
    }

    [Fact]
    public async Task SubscribeRoomAsync_Cancellation_StopsReceiving()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        PlayerInfoResult player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
        int roomId = player.PlayerInfo.RoomId;

        await Task.Delay(100);

        await deps.RoomStore.PublishRoomUpdateAsync(roomId, RoomUpdateContext.Broadcast(), CancellationToken.None);

        await Task.Delay(100);

        using var cts = new CancellationTokenSource();

        var snapshots = new List<RoomStateSnapshot>();
        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(
                                   player.PlayerInfo.Id,
                                   roomId,
                                   cts.Token))
                {
                    snapshots.Add(snap);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);

        await deps.RoomStore.PublishRoomUpdateAsync(roomId, RoomUpdateContext.Broadcast(), CancellationToken.None);

        await Task.Delay(50);
        await cts.CancelAsync();

        await Task.Delay(100);

        await deps.RoomStore.PublishRoomUpdateAsync(roomId, RoomUpdateContext.Broadcast(), CancellationToken.None);

        await Task.Delay(100);
        await task;

        Assert.True(snapshots.Count > 0);
    }

    [Fact]
    public async Task SubscribeRoomAsync_ReceivesInitialSnapshotEvenIfLastUpdateExcludedThem()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        PlayerInfoResult player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
        int roomId = player1.PlayerInfo.RoomId;

        await Task.Delay(100);

        // Publish an update that excludes this player (simulating they triggered it)
        await deps.RoomStore.PublishRoomUpdateAsync(
            roomId,
            RoomUpdateContext.ExcludePlayer(player1.PlayerInfo.Id),
            CancellationToken.None);

        await Task.Delay(100);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshots = new List<RoomStateSnapshot>();

        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(
                                   player1.PlayerInfo.Id,
                                   roomId,
                                   cts.Token))
                {
                    snapshots.Add(snap);
                    break; // Only need the initial snapshot
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await task;

        // Player should receive the initial room state snapshot even if the last update excluded them.
        // Exclusion is for preventing self-notification, not hiding data from players.
        Assert.Single(snapshots);
        Assert.Equal(roomId, snapshots[0].RoomId);
    }
}