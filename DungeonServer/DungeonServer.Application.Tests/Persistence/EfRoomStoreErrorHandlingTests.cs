using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Infrastructure.EntityFramework;
using DungeonServer.Infrastructure.EntityFramework.Stores.Player;
using DungeonServer.Infrastructure.EntityFramework.Stores.Rooms;
using DungeonServer.Infrastructure.Messaging.Rooms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DungeonServer.Application.Tests.Persistence;

public sealed class EfRoomStoreErrorHandlingTests : IDisposable
{
    private readonly DbContextOptions<DungeonDbContext> _options;
    private readonly DungeonDbContext _dbContext;
    private readonly EfRoomStore _roomStore;
    private readonly EfPlayerStore _playerStore;
    private readonly RoomSubscriptionRegistry _registry;

    public EfRoomStoreErrorHandlingTests()
    {
        _options = new DbContextOptionsBuilder<DungeonDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new DungeonDbContext(_options);
        _playerStore = new EfPlayerStore(_dbContext);
        
        _registry = new RoomSubscriptionRegistry(_playerStore);
        _roomStore = new EfRoomStore(_dbContext, _registry);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AddPlayerToRoom_ThrowsKeyNotFound_WhenPlayerDoesNotExist()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roomStore.AddPlayerToRoomAsync(room.RoomId, 99999, CancellationToken.None));
    }

    [Fact]
    public async Task RemovePlayerFromRoom_ThrowsArgumentException_WhenPlayerNotInRoom()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);
        
        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(new Location(5f, 5f), CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _roomStore.RemovePlayerFromRoomAsync(room.RoomId, player.PlayerId, CancellationToken.None));
    }

    [Fact]
    public async Task LinkRooms_ThrowsKeyNotFound_WhenRoomDoesNotExist()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roomStore.LinkRoomsAsync(room.RoomId, 99999, Direction.East, CancellationToken.None));
    }

    [Fact]
    public async Task LinkRooms_ThrowsArgumentException_WhenLinkingRoomToItself()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _roomStore.LinkRoomsAsync(room.RoomId, room.RoomId, Direction.East, CancellationToken.None));
    }

    [Fact]
    public async Task SwapRooms_ThrowsKeyNotFound_WhenFromRoomDoesNotExist()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);
        
        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(new Location(5f, 5f), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roomStore.SwapRoomsAsync(player.PlayerId, 99999, room.RoomId, CancellationToken.None));
    }

    [Fact]
    public async Task SwapRooms_ThrowsKeyNotFound_WhenToRoomDoesNotExist()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);
        
        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(new Location(5f, 5f), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roomStore.SwapRoomsAsync(player.PlayerId, room.RoomId, 99999, CancellationToken.None));
    }

    [Fact]
    public async Task SwapRooms_ThrowsKeyNotFound_WhenPlayerNotInFromRoom()
    {
        RoomStateSnapshot roomA = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);
        RoomStateSnapshot roomB = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);
        
        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(new Location(5f, 5f), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _roomStore.SwapRoomsAsync(player.PlayerId, roomA.RoomId, roomB.RoomId, CancellationToken.None));
    }

    [Fact]
    public async Task AddPlayerToRoom_AlreadyInRoom_ReturnsSnapshotWithoutAddingDuplicate()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10), CancellationToken.None);
        
        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(new Location(5f, 5f), CancellationToken.None);

        RoomStateSnapshot first = await _roomStore.AddPlayerToRoomAsync(room.RoomId, player.PlayerId, CancellationToken.None);
        RoomStateSnapshot second = await _roomStore.AddPlayerToRoomAsync(room.RoomId, player.PlayerId, CancellationToken.None);

        Assert.Single(first.Players);
        Assert.Single(second.Players);
    }
}
