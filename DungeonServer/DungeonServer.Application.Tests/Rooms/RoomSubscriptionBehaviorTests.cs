using System.Linq;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms;

public class RoomSubscriptionBehaviorTests
{
    [Fact]
    public async Task SubscribeAsync_NonExistentRoom_NoEmissions()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var player = await deps.PlayerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var snapshots = new List<RoomPlayerUpdate>();

        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snapshot in deps.RoomStore.SubscribeRoomAsync(player.PlayerId, 999, cts.Token))
                {
                    snapshots.Add(snapshot);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);
        await cts.CancelAsync();
        await task;

        Assert.Empty(snapshots);
    }

    [Fact]
    public async Task SubscribeAsync_MultipleRooms_IndependentStreams()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room1 = new RoomState(RoomType.Combat, 10, 8);
        var room2 = new RoomState(RoomType.Treasure, 15, 12);
        RoomStateSnapshot createdRoom1 = await deps.RoomStore.CreateRoomAsync(room1, CancellationToken.None);
        RoomStateSnapshot createdRoom2 = await deps.RoomStore.CreateRoomAsync(room2, CancellationToken.None);

        var subscriber1 = await deps.PlayerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);
        var subscriber2 = await deps.PlayerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshots1 = new List<RoomPlayerUpdate>();
        var snapshots2 = new List<RoomPlayerUpdate>();

        Task task1 = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snapshot in deps.RoomStore.SubscribeRoomAsync(
                                   subscriber1.PlayerId,
                                   createdRoom1.RoomId,
                                   cts.Token))
                {
                    snapshots1.Add(snapshot);
                    if (snapshots1.Count >= 2) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        Task task2 = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snapshot in deps.RoomStore.SubscribeRoomAsync(
                                   subscriber2.PlayerId,
                                   createdRoom2.RoomId,
                                   cts.Token))
                {
                    snapshots2.Add(snapshot);
                    if (snapshots2.Count >= 2) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        var player100 = await deps.PlayerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);
        var player200 = await deps.PlayerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(createdRoom1.RoomId, player100.PlayerId, CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(createdRoom2.RoomId, player200.PlayerId, CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();

        await Task.WhenAll(task1, task2);

        Assert.True(snapshots1.Count >= 2);
        Assert.True(snapshots2.Count >= 2);

        var lastSnapshot1 = snapshots1[^1];
        var lastSnapshot2 = snapshots2[^1];

        Assert.Equal(createdRoom1.RoomId, lastSnapshot1.RoomId);
        Assert.Contains(player100.PlayerId, lastSnapshot1.Players.Select(p => p.PlayerId));
        Assert.DoesNotContain(player200.PlayerId, lastSnapshot1.Players.Select(p => p.PlayerId));

        Assert.Equal(createdRoom2.RoomId, lastSnapshot2.RoomId);
        Assert.Contains(player200.PlayerId, lastSnapshot2.Players.Select(p => p.PlayerId));
        Assert.DoesNotContain(player100.PlayerId, lastSnapshot2.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SubscribeAsync_MultiplePlayerAdditions_ReceivedInOrder()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        var room = new RoomState(RoomType.Combat, 10, 8);
        RoomStateSnapshot createdRoom = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        var subscriber = await deps.PlayerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshots = new List<RoomPlayerUpdate>();

        Task subscribeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snapshot in deps.RoomStore.SubscribeRoomAsync(
                                   subscriber.PlayerId,
                                   createdRoom.RoomId,
                                   cts.Token))
                {
                    snapshots.Add(snapshot);
                    if (snapshots.Count >= 4) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        var player10 = await deps.PlayerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);
        var player20 = await deps.PlayerStore.CreatePlayerAsync(new Location(1, 0), CancellationToken.None);
        var player30 = await deps.PlayerStore.CreatePlayerAsync(new Location(2, 0), CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(createdRoom.RoomId, player10.PlayerId, CancellationToken.None);
        await Task.Delay(25);
        await deps.RoomStore.AddPlayerToRoomAsync(createdRoom.RoomId, player20.PlayerId, CancellationToken.None);
        await Task.Delay(25);
        await deps.RoomStore.AddPlayerToRoomAsync(createdRoom.RoomId, player30.PlayerId, CancellationToken.None);

        await subscribeTask;

        Assert.Equal(4, snapshots.Count);
        Assert.Empty(snapshots[0].Players);
        Assert.Single(snapshots[1].Players);
        Assert.Equal(2, snapshots[2].Players.Count);
        Assert.Equal(3, snapshots[3].Players.Count);
        Assert.Equal(new[] { player10.PlayerId }, snapshots[1].Players.Select(p => p.PlayerId).ToArray());
        Assert.Equal(new[] { player10.PlayerId, player20.PlayerId }, snapshots[2].Players.Select(p => p.PlayerId).ToArray());
        Assert.Equal(new[] { player10.PlayerId, player20.PlayerId, player30.PlayerId }, snapshots[3].Players.Select(p => p.PlayerId).ToArray());
    }
}
