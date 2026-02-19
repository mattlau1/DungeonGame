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
    public async Task CreateRoomAsync_RespectsCancellationToken()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), cts.Token));
    }

    [Fact]
    public async Task AddPlayerToRoomAsync_Throws_WhenRoomDoesNotExist()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            deps.RoomStore.AddPlayerToRoomAsync(999, 1, CancellationToken.None));
    }

    [Fact]
    public async Task RemovePlayerFromRoomAsync_Throws_WhenRoomDoesNotExist()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            deps.RoomStore.RemovePlayerFromRoomAsync(999, 1, CancellationToken.None));
    }

    [Fact]
    public async Task AddPlayerToRoomAsync_AddsPlayerAndReturnsSnapshot()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot created = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        RoomStateSnapshot updated = await deps.RoomStore.AddPlayerToRoomAsync(created.RoomId, 42, CancellationToken.None);

        Assert.Equal(created.RoomId, updated.RoomId);
        Assert.Contains(42, updated.PlayerIds);
    }

    [Fact]
    public async Task RemovePlayerFromRoomAsync_RemovesPlayerAndReturnsSnapshot()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot created = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);
        await deps.RoomStore.AddPlayerToRoomAsync(created.RoomId, 42, CancellationToken.None);

        RoomStateSnapshot updated = await deps.RoomStore.RemovePlayerFromRoomAsync(created.RoomId, 42, CancellationToken.None);

        Assert.Equal(created.RoomId, updated.RoomId);
        Assert.DoesNotContain(42, updated.PlayerIds);
    }

    [Fact]
    public async Task AddPlayerToRoomAsync_RespectsCancellationToken()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot created = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            deps.RoomStore.AddPlayerToRoomAsync(created.RoomId, 1, cts.Token));
    }

    [Fact]
    public async Task AddPlayerToRoomAsync_IsAtomic_NoLostPlayers()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

        RoomStateSnapshot created = await deps.RoomStore.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        const int playerCount = 200;

        Task[] tasks = Enumerable.Range(0, playerCount)
            .Select(i => deps.RoomStore.AddPlayerToRoomAsync(created.RoomId, i, CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        RoomStateSnapshot? snapshot = await deps.RoomStore.GetRoomAsync(created.RoomId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(playerCount, snapshot!.PlayerIds.Count);
    }
}