using DungeonServer.Application.Dungeon.Rooms.Models;
using DungeonServer.Application.Dungeon.Rooms.Storage;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms.Storage;

public sealed class InMemoryRoomStoreTests
{
    private static RoomState NewRoom()
        => new()
        {
            RoomId = 0,
            Width = 10,
            Height = 8,
            RoomType = RoomType.Combat
        };

    [Fact]
    public async Task CreateRoomAsync_AssignsId_AndReturnsSnapshot()
    {
        var store = new InMemoryRoomStore();

        RoomStateSnapshot snapshot = await store.CreateRoomAsync(NewRoom(), CancellationToken.None);

        Assert.True(snapshot.RoomId > 0);
        Assert.Equal(RoomType.Combat, snapshot.RoomType);
        Assert.Equal(10, snapshot.Width);
        Assert.Equal(8, snapshot.Height);
    }

    [Fact]
    public async Task CreateRoomAsync_MultipleRooms_GeneratesUniqueIds()
    {
        var store = new InMemoryRoomStore();

        RoomStateSnapshot a = await store.CreateRoomAsync(NewRoom(), CancellationToken.None);
        RoomStateSnapshot b = await store.CreateRoomAsync(NewRoom(), CancellationToken.None);

        Assert.NotEqual(a.RoomId, b.RoomId);
    }

    [Fact]
    public async Task CreateRoomAsync_ThrowsIfRoomIdIsNotZero()
    {
        var store = new InMemoryRoomStore();

        var room = NewRoom();
        room.RoomId = 123;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateRoomAsync(room, CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomAsync_ReturnsNull_WhenRoomDoesNotExist()
    {
        var store = new InMemoryRoomStore();

        RoomStateSnapshot? snapshot = await store.GetRoomAsync(roomId: 999, CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task UpdateRoomAsync_Throws_WhenRoomDoesNotExist()
    {
        var store = new InMemoryRoomStore();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.UpdateRoomAsync(999, _ => { }, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRoomAsync_MutatesState_AndReturnsUpdatedSnapshot()
    {
        var store = new InMemoryRoomStore();

        RoomStateSnapshot created = await store.CreateRoomAsync(NewRoom(), CancellationToken.None);

        RoomStateSnapshot updated = await store.UpdateRoomAsync(
            created.RoomId,
            state =>
            {
                state.Width = 42;
                state.RoomType = RoomType.Boss;
            },
            CancellationToken.None);

        Assert.Equal(created.RoomId, updated.RoomId);
        Assert.Equal(42, updated.Width);
        Assert.Equal(RoomType.Boss, updated.RoomType);
    }

    [Fact]
    public async Task CreateRoomAsync_RespectsCancellationToken()
    {
        var store = new InMemoryRoomStore();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.CreateRoomAsync(NewRoom(), cts.Token));
    }

    [Fact]
    public async Task UpdateRoomAsync_RespectsCancellationTokenWhileWaiting()
    {
        var store = new InMemoryRoomStore();

        var created = await store.CreateRoomAsync(NewRoom(), CancellationToken.None);

        // Hold the gate with one update; cancel another update while it waits.
        using var gateHeld = new ManualResetEventSlim(false);

        var holder = Task.Run(() => store.UpdateRoomAsync(
            created.RoomId,
            _ =>
            {
                gateHeld.Set();
                Thread.Sleep(250);
            },
            CancellationToken.None));

        gateHeld.Wait();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.UpdateRoomAsync(created.RoomId, _ => { }, cts.Token));

        await holder;
    }

    [Fact]
    public async Task UpdateRoomAsync_IsAtomic_PerRoom_NoLostUpdates()
    {
        var store = new InMemoryRoomStore();
        var created = await store.CreateRoomAsync(NewRoom(), CancellationToken.None);

        const int updates = 200;

        Task<RoomStateSnapshot>[] tasks = Enumerable.Range(0, updates)
            .Select(_ => store.UpdateRoomAsync(created.RoomId, s => s.Width += 1, CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        RoomStateSnapshot? snapshot = await store.GetRoomAsync(created.RoomId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(10 + updates, snapshot!.Width);
    }
}