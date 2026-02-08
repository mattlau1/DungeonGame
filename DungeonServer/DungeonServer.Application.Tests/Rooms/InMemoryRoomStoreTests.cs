using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Player.Storage;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms;

public sealed class InMemoryRoomStoreTests
{
    private static RoomState GenerateNewRoom()
    {
        return new RoomState(RoomType.Combat, 10, 8);
    }

    [Fact]
    public async Task CreateRoomAsync_AssignsId_AndReturnsSnapshot()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);

        RoomState room = GenerateNewRoom();
        RoomStateSnapshot snapshot = await store.CreateRoomAsync(room, CancellationToken.None);

        Assert.True(snapshot.RoomId > 0);
        Assert.Equal(RoomType.Combat, snapshot.RoomType);
        Assert.Equal(room.Width, snapshot.Width);
        Assert.Equal(room.Height, snapshot.Height);
    }

    [Fact]
    public async Task CreateRoomAsync_MultipleRooms_GeneratesUniqueIds()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);

        RoomStateSnapshot a = await store.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);
        RoomStateSnapshot b = await store.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        Assert.NotEqual(a.RoomId, b.RoomId);
    }

    [Fact]
    public async Task CreateRoomAsync_ThrowsIfRoomIdIsNotZero()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);

        RoomState room = GenerateNewRoom();
        room.RoomId = 123;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateRoomAsync(room, CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomAsync_ReturnsNull_WhenRoomDoesNotExist()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);

        RoomStateSnapshot? snapshot = await store.GetRoomAsync(roomId: 999, CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task UpdateRoomAsync_Throws_WhenRoomDoesNotExist()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.UpdateRoomAsync(999, _ => { }, RoomUpdateContext.Broadcast(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRoomAsync_MutatesState_AndReturnsUpdatedSnapshot()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);

        RoomStateSnapshot created = await store.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        RoomStateSnapshot updated = await store.UpdateRoomAsync(
            created.RoomId,
            state =>
            {
                state.Width = 42;
                state.RoomType = RoomType.Boss;
            },
            RoomUpdateContext.Broadcast(),
            CancellationToken.None);

        Assert.Equal(created.RoomId, updated.RoomId);
        Assert.Equal(42, updated.Width);
        Assert.Equal(RoomType.Boss, updated.RoomType);
    }

    [Fact]
    public async Task CreateRoomAsync_RespectsCancellationToken()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.CreateRoomAsync(GenerateNewRoom(), cts.Token));
    }

    [Fact]
    public async Task UpdateRoomAsync_RespectsCancellationTokenWhileWaiting()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);

        RoomStateSnapshot created = await store.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        // Hold the gate with one update; cancel another update while it waits.
        using var gateHeld = new ManualResetEventSlim(false);

        Task<RoomStateSnapshot> holder = Task.Run(() => store.UpdateRoomAsync(
            created.RoomId,
            _ =>
            {
                gateHeld.Set();
                Thread.Sleep(250);
            },
            RoomUpdateContext.Broadcast(),
            CancellationToken.None));

        gateHeld.Wait();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.UpdateRoomAsync(created.RoomId, _ => { }, RoomUpdateContext.Broadcast(), cts.Token));

        await holder;
    }

    [Fact]
    public async Task UpdateRoomAsync_IsAtomic_PerRoom_NoLostUpdates()
    {
        var playerStore = new InMemoryPlayerStore();
        var subscriptionRegistry = new RoomSubscriptionRegistry(playerStore);
        var store = new InMemoryRoomStore(subscriptionRegistry);
        RoomStateSnapshot created = await store.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        const int updates = 200;

        Task<RoomStateSnapshot>[] tasks = Enumerable.Range(0, updates)
            .Select(_ => store.UpdateRoomAsync(created.RoomId, s => s.Width += 1, RoomUpdateContext.Broadcast(),
                CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        RoomStateSnapshot? snapshot = await store.GetRoomAsync(created.RoomId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(10 + updates, snapshot!.Width);
    }
}