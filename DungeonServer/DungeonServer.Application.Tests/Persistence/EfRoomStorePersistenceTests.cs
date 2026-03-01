using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Infrastructure.Caching.Player;
using DungeonServer.Infrastructure.EntityFramework;
using DungeonServer.Infrastructure.EntityFramework.Stores.Player;
using DungeonServer.Infrastructure.EntityFramework.Stores.Rooms;
using DungeonServer.Infrastructure.Messaging.Rooms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using PlayerInfo = DungeonGame.Core.PlayerInfo;

namespace DungeonServer.Application.Tests.Persistence;

public sealed class EfRoomStorePersistenceTests : IDisposable
{
    private readonly DbContextOptions<DungeonDbContext> _options;
    private readonly Mock<IPlayerCache> _mockPlayerCache;
    private readonly IDbContextFactory<DungeonDbContext> _contextFactory;
    private readonly EfRoomStore _roomStore;
    private readonly EfPlayerStore _playerStore;
    private readonly InMemoryRoomSubscriptionRegistry _registry;

    public EfRoomStorePersistenceTests()
    {
        _options = new DbContextOptionsBuilder<DungeonDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _mockPlayerCache = new Mock<IPlayerCache>();
        _mockPlayerCache
            .Setup(x => x.GetOrSetAsync(It.IsAny<int>(), It.IsAny<Func<Task<PlayerInfo>>>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(async (int id, Func<Task<PlayerInfo>> factory, TimeSpan? expiry, CancellationToken ct) => await factory());

        _contextFactory = new TestDbContextFactory(_options);
        _playerStore = new EfPlayerStore(_contextFactory, _mockPlayerCache.Object);

        _registry = new InMemoryRoomSubscriptionRegistry();
        _roomStore = new EfRoomStore(_contextFactory, _registry);
    }

    public void Dispose()
    {
    }

    private class TestDbContextFactory : IDbContextFactory<DungeonDbContext>
    {
        private readonly DbContextOptions<DungeonDbContext> _options;

        public TestDbContextFactory(DbContextOptions<DungeonDbContext> options)
        {
            _options = options;
        }

        public DungeonDbContext CreateDbContext()
        {
            return new DungeonDbContext(_options);
        }

        public Task<DungeonDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DungeonDbContext(_options));
        }
    }

    [Fact]
    public async Task CreateRoom_Persists_AcrossNewContext()
    {
        RoomState room = new RoomState(RoomType.Combat, 32, 24);
        RoomStateSnapshot created = await _roomStore.CreateRoomAsync(room, CancellationToken.None);

        var newFactory = new TestDbContextFactory(_options);
        var newRegistry = new InMemoryRoomSubscriptionRegistry();
        var newStore = new EfRoomStore(newFactory, newRegistry);

        RoomStateSnapshot? retrieved = await newStore.GetRoomAsync(created.RoomId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(created.RoomId, retrieved.RoomId);
        Assert.Equal(RoomType.Combat, retrieved.RoomType);
        Assert.Equal(32, retrieved.Width);
        Assert.Equal(24, retrieved.Height);
    }

    [Fact]
    public async Task AddPlayerToRoom_Persists_AcrossNewContext()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10),
            CancellationToken.None);

        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(new Location(5f, 5f), CancellationToken.None);

        await _roomStore.AddPlayerToRoomAsync(room.RoomId, player.PlayerId, CancellationToken.None);

        var newFactory = new TestDbContextFactory(_options);
        var newRegistry = new InMemoryRoomSubscriptionRegistry();
        var newStore = new EfRoomStore(newFactory, newRegistry);

        RoomStateSnapshot? retrieved = await newStore.GetRoomAsync(room.RoomId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Contains(player.PlayerId, retrieved.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task RemovePlayerFromRoom_Persists_AcrossNewContext()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10),
            CancellationToken.None);

        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(new Location(5f, 5f), CancellationToken.None);

        await _roomStore.AddPlayerToRoomAsync(room.RoomId, player.PlayerId, CancellationToken.None);
        await _roomStore.RemovePlayerFromRoomAsync(room.RoomId, player.PlayerId, CancellationToken.None);

        var newFactory = new TestDbContextFactory(_options);
        var newRegistry = new InMemoryRoomSubscriptionRegistry();
        var newStore = new EfRoomStore(newFactory, newRegistry);

        RoomStateSnapshot? retrieved = await newStore.GetRoomAsync(room.RoomId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.DoesNotContain(player.PlayerId, retrieved.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task LinkRooms_Persists_AcrossNewContext()
    {
        RoomStateSnapshot roomA = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10),
            CancellationToken.None);
        RoomStateSnapshot roomB = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10),
            CancellationToken.None);

        await _roomStore.LinkRoomsAsync(roomA.RoomId, roomB.RoomId, Direction.East, CancellationToken.None);

        var newFactory = new TestDbContextFactory(_options);
        var newRegistry = new InMemoryRoomSubscriptionRegistry();
        var newStore = new EfRoomStore(newFactory, newRegistry);

        RoomStateSnapshot? retrievedA = await newStore.GetRoomAsync(roomA.RoomId, CancellationToken.None);
        RoomStateSnapshot? retrievedB = await newStore.GetRoomAsync(roomB.RoomId, CancellationToken.None);

        Assert.NotNull(retrievedA);
        Assert.NotNull(retrievedB);
        Assert.Contains(Direction.East, retrievedA.Exits.Keys);
        Assert.Equal(roomB.RoomId, retrievedA.Exits[Direction.East]);
        Assert.Contains(Direction.West, retrievedB.Exits.Keys);
        Assert.Equal(roomA.RoomId, retrievedB.Exits[Direction.West]);
    }

    [Fact]
    public async Task SwapRooms_Persists_AcrossNewContext()
    {
        RoomStateSnapshot roomA = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10),
            CancellationToken.None);
        RoomStateSnapshot roomB = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10),
            CancellationToken.None);

        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(new Location(5f, 5f), CancellationToken.None);

        await _roomStore.AddPlayerToRoomAsync(roomA.RoomId, player.PlayerId, CancellationToken.None);
        await _roomStore.SwapRoomsAsync(player.PlayerId, roomA.RoomId, roomB.RoomId, CancellationToken.None);

        var newFactory = new TestDbContextFactory(_options);
        var newRegistry = new InMemoryRoomSubscriptionRegistry();
        var newStore = new EfRoomStore(newFactory, newRegistry);

        RoomStateSnapshot? retrievedA = await newStore.GetRoomAsync(roomA.RoomId, CancellationToken.None);
        RoomStateSnapshot? retrievedB = await newStore.GetRoomAsync(roomB.RoomId, CancellationToken.None);

        Assert.NotNull(retrievedA);
        Assert.NotNull(retrievedB);
        Assert.DoesNotContain(player.PlayerId, retrievedA.Players.Select(p => p.PlayerId));
        Assert.Contains(player.PlayerId, retrievedB.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task MultiplePlayersInRoom_Persists_AcrossNewContext()
    {
        RoomStateSnapshot room = await _roomStore.CreateRoomAsync(
            new RoomState(RoomType.Combat, 10, 10),
            CancellationToken.None);

        var players = new List<PlayerSnapshot>();
        for (int i = 0; i < 5; i++)
        {
            PlayerSnapshot p = await _playerStore.CreatePlayerAsync(
                new Location((float)i, (float)i),
                CancellationToken.None);
            players.Add(p);
            await _roomStore.AddPlayerToRoomAsync(room.RoomId, p.PlayerId, CancellationToken.None);
        }

        var newFactory = new TestDbContextFactory(_options);
        var newRegistry = new InMemoryRoomSubscriptionRegistry();
        var newStore = new EfRoomStore(newFactory, newRegistry);

        RoomStateSnapshot? retrieved = await newStore.GetRoomAsync(room.RoomId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(5, retrieved.Players.Count);
        foreach (var p in players)
        {
            Assert.Contains(p.PlayerId, retrieved.Players.Select(p => p.PlayerId));
        }
    }

    [Fact]
    public async Task PlayerInInvalidRoom_AfterCreation_HasInvalidRoomId()
    {
        PlayerSnapshot created = await _playerStore.CreatePlayerAsync(new Location(1f, 1f), CancellationToken.None);

        var newFactory = new TestDbContextFactory(_options);
        var newStore = new EfPlayerStore(newFactory, _mockPlayerCache.Object);

        PlayerSnapshot? retrieved = await newStore.GetPlayerAsync(created.PlayerId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(RoomConstants.InvalidRoomId, retrieved.RoomId);
    }
}
