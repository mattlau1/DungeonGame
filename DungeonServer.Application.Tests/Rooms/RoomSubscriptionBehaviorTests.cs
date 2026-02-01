using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms;

public class RoomSubscriptionBehaviorTests
{
    [Fact]
    public async Task SubscribeAsync_ReceivesPublishedUpdates()
    {
        var subscriptionRegistry = new RoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(subscriptionRegistry);

        var room = new RoomState(RoomType.Combat, 10, 8);
        RoomStateSnapshot createdRoom = await roomStore.CreateRoomAsync(room, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshots = new List<RoomStateSnapshot>();

        Task subscribeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(createdRoom.RoomId, 1,
                                   cts.Token))
                {
                    snapshots.Add(snapshot);
                    if (snapshots.Count >= 2)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        await roomStore.UpdateRoomAsync(createdRoom.RoomId, r => r.Width = 20, RoomUpdateContext.Broadcast(),
            CancellationToken.None);

        await Task.Delay(50);
        await cts.CancelAsync();
        await subscribeTask;

        Assert.True(snapshots.Count >= 2);
        Assert.Equal(10, snapshots[0].Width);
        Assert.Equal(20, snapshots[1].Width);
    }

    [Fact]
    public async Task SubscribeAsync_ExcludesPlayerWhenSpecified_AfterSubscribe()
    {
        var subscriptionRegistry = new RoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(subscriptionRegistry);

        var room = new RoomState(RoomType.Combat, 10, 8);
        RoomStateSnapshot createdRoom = await roomStore.CreateRoomAsync(room, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshots = new List<RoomStateSnapshot>();

        Task subscribeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(createdRoom.RoomId, 1,
                                   cts.Token))
                {
                    snapshots.Add(snapshot);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        await roomStore.UpdateRoomAsync(createdRoom.RoomId, r => r.Width = 20, RoomUpdateContext.ExcludePlayer(1),
            CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();
        await subscribeTask;

        Assert.All(snapshots, s => Assert.Equal(10, s.Width));
    }

    [Fact]
    public async Task SubscribeAsync_MultipleSubscribers_AllReceiveBroadcasts()
    {
        var subscriptionRegistry = new RoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(subscriptionRegistry);

        var room = new RoomState(RoomType.Combat, 10, 8);
        RoomStateSnapshot createdRoom = await roomStore.CreateRoomAsync(room, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshots1 = new List<RoomStateSnapshot>();
        var snapshots2 = new List<RoomStateSnapshot>();

        Task task1 = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(createdRoom.RoomId, 1,
                                   cts.Token))
                {
                    snapshots1.Add(snapshot);
                    if (snapshots1.Count >= 1)
                        break;
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
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(createdRoom.RoomId, 2,
                                   cts.Token))
                {
                    snapshots2.Add(snapshot);
                    if (snapshots2.Count >= 1)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        await roomStore.UpdateRoomAsync(createdRoom.RoomId, r => r.Width = 20, RoomUpdateContext.Broadcast(),
            CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();

        await Task.WhenAll(task1, task2);

        Assert.True(snapshots1.Count >= 1);
        Assert.True(snapshots2.Count >= 1);
    }

    [Fact]
    public async Task SubscribeAsync_NonExistentRoom_NoEmissions()
    {
        var subscriptionRegistry = new RoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(subscriptionRegistry);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var snapshots = new List<RoomStateSnapshot>();

        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(999, 1, cts.Token))
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
    public async Task SubscribeAsync_MultipleUpdates_ReceivedInOrder()
    {
        var subscriptionRegistry = new RoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(subscriptionRegistry);

        var room = new RoomState(RoomType.Combat, 10, 8);
        RoomStateSnapshot createdRoom = await roomStore.CreateRoomAsync(room, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var snapshots = new List<RoomStateSnapshot>();

        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(createdRoom.RoomId, 1,
                                   cts.Token))
                {
                    snapshots.Add(snapshot);
                    if (snapshots.Count >= 4)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        await roomStore.UpdateRoomAsync(createdRoom.RoomId, r => r.Width = 15, RoomUpdateContext.Broadcast(),
            CancellationToken.None);
        await Task.Delay(25);
        await roomStore.UpdateRoomAsync(createdRoom.RoomId, r => r.Width = 20, RoomUpdateContext.Broadcast(),
            CancellationToken.None);
        await Task.Delay(25);
        await roomStore.UpdateRoomAsync(createdRoom.RoomId, r => r.Width = 25, RoomUpdateContext.Broadcast(),
            CancellationToken.None);

        await task;

        Assert.Equal(4, snapshots.Count);
        Assert.Equal(10, snapshots[0].Width);
        Assert.Equal(15, snapshots[1].Width);
        Assert.Equal(20, snapshots[2].Width);
        Assert.Equal(25, snapshots[3].Width);
    }

    [Fact]
    public async Task SubscribeAsync_MultipleRooms_IndependentStreams()
    {
        var subscriptionRegistry = new RoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(subscriptionRegistry);

        var room1 = new RoomState(RoomType.Combat, 10, 8);
        var room2 = new RoomState(RoomType.Treasure, 15, 12);
        RoomStateSnapshot createdRoom1 = await roomStore.CreateRoomAsync(room1, CancellationToken.None);
        RoomStateSnapshot createdRoom2 = await roomStore.CreateRoomAsync(room2, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshots1 = new List<RoomStateSnapshot>();
        var snapshots2 = new List<RoomStateSnapshot>();

        Task task1 = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(createdRoom1.RoomId, 1,
                                   cts.Token))
                {
                    snapshots1.Add(snapshot);
                    if (snapshots1.Count >= 1)
                        break;
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
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(createdRoom2.RoomId, 2,
                                   cts.Token))
                {
                    snapshots2.Add(snapshot);
                    if (snapshots2.Count >= 1)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        await roomStore.UpdateRoomAsync(createdRoom1.RoomId, r => r.Width = 20, RoomUpdateContext.Broadcast(),
            CancellationToken.None);
        await roomStore.UpdateRoomAsync(createdRoom2.RoomId, r => r.Width = 25, RoomUpdateContext.Broadcast(),
            CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();

        await Task.WhenAll(task1, task2);

        Assert.All(snapshots1, s => Assert.Equal(createdRoom1.RoomId, s.RoomId));
        Assert.All(snapshots2, s => Assert.Equal(createdRoom2.RoomId, s.RoomId));
    }

    [Fact]
    public async Task SubscribeAsync_ExcludeOtherPlayers_DoesNotExcludeSubscriber()
    {
        var subscriptionRegistry = new RoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(subscriptionRegistry);

        var room = new RoomState(RoomType.Combat, 10, 8);
        RoomStateSnapshot createdRoom = await roomStore.CreateRoomAsync(room, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshots = new List<RoomStateSnapshot>();

        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomStateSnapshot snapshot in roomStore.SubscribeRoomAsync(createdRoom.RoomId, 1,
                                   cts.Token))
                {
                    snapshots.Add(snapshot);
                    if (snapshots.Count >= 2)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        await roomStore.UpdateRoomAsync(createdRoom.RoomId, r => r.Width = 20, RoomUpdateContext.ExcludePlayer(2),
            CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();
        await task;

        Assert.True(snapshots.Count >= 2);
        Assert.Equal(10, snapshots[0].Width);
        Assert.Equal(20, snapshots[1].Width);
    }
}