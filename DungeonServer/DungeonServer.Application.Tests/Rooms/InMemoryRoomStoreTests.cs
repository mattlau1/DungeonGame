using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
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
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomState room = GenerateNewRoom();
        RoomStateSnapshot snapshot = await deps.RoomStore.CreateRoomAsync(room, CancellationToken.None);

        Assert.True(snapshot.RoomId > 0);
        Assert.Equal(RoomType.Combat, snapshot.RoomType);
        Assert.Equal(room.Width, snapshot.Width);
        Assert.Equal(room.Height, snapshot.Height);
    }

    [Fact]
    public async Task CreateRoomAsync_MultipleRooms_GeneratesUniqueIds()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot a = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);
        RoomStateSnapshot b = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        Assert.NotEqual(a.RoomId, b.RoomId);
    }

    [Fact]
    public async Task CreateRoomAsync_ThrowsIfRoomIdIsNotZero()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomState room = GenerateNewRoom();
        room.RoomId = 123;

        await Assert.ThrowsAsync<ArgumentException>(() => deps.RoomStore.CreateRoomAsync(room, CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomAsync_ReturnsNull_WhenRoomDoesNotExist()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot? snapshot = await deps.RoomStore.GetRoomAsync(roomId: 999, CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task UpdateRoomAsync_Throws_WhenRoomDoesNotExist()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => deps.RoomStore.UpdateRoomAsync(
            999,
            _ => { },
            RoomUpdateContext.Broadcast(),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRoomAsync_MutatesState_AndReturnsUpdatedSnapshot()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot created = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        RoomStateSnapshot updated = await deps.RoomStore.UpdateRoomAsync(
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
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), cts.Token));
    }

    [Fact]
    public async Task UpdateRoomAsync_RespectsCancellationTokenWhileWaiting()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot created = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        // Hold the gate with one update; cancel another update while it waits.
        using var gateHeld = new ManualResetEventSlim(false);

        Task<RoomStateSnapshot> holder = Task.Run(() => deps.RoomStore.UpdateRoomAsync(
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
            deps.RoomStore.UpdateRoomAsync(created.RoomId, _ => { }, RoomUpdateContext.Broadcast(), cts.Token));

        await holder;
    }

    [Fact]
    public async Task UpdateRoomAsync_IsAtomic_PerRoom_NoLostUpdates()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot created = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        const int updates = 200;

        Task<RoomStateSnapshot>[] tasks = Enumerable.Range(0, updates).Select(_ => deps.RoomStore.UpdateRoomAsync(
            created.RoomId,
            s => s.Width += 1,
            RoomUpdateContext.Broadcast(),
            CancellationToken.None)).ToArray();

        await Task.WhenAll(tasks);

        RoomStateSnapshot? snapshot = await deps.RoomStore.GetRoomAsync(created.RoomId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(10 + updates, snapshot!.Width);
    }
}