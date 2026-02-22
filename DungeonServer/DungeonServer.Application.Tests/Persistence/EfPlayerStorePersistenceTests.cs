using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Infrastructure.EntityFramework;
using DungeonServer.Infrastructure.EntityFramework.Stores.Player;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DungeonServer.Application.Tests.Persistence;

public sealed class EfPlayerStorePersistenceTests : IDisposable
{
    private readonly DbContextOptions<DungeonDbContext> _options;
    private readonly DungeonDbContext _dbContext;
    private readonly EfPlayerStore _playerStore;

    public EfPlayerStorePersistenceTests()
    {
        _options = new DbContextOptionsBuilder<DungeonDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DungeonDbContext(_options);
        _playerStore = new EfPlayerStore(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreatePlayer_Persists_AcrossNewContext()
    {
        var location = new Location(5f, 10f);
        PlayerSnapshot created = await _playerStore.CreatePlayerAsync(location, CancellationToken.None);

        using var newContext = new DungeonDbContext(_options);
        var newStore = new EfPlayerStore(newContext);

        PlayerSnapshot? retrieved = await newStore.GetPlayerAsync(created.PlayerId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(created.PlayerId, retrieved.PlayerId);
        Assert.Equal(location.X, retrieved.Location.X);
        Assert.Equal(location.Y, retrieved.Location.Y);
    }

    [Fact]
    public async Task UpdateLocation_Persists_AcrossNewContext()
    {
        PlayerSnapshot created = await _playerStore.CreatePlayerAsync(new Location(1f, 1f), CancellationToken.None);

        var newLocation = new Location(100f, 200f);
        await _playerStore.UpdateLocationAsync(created.PlayerId, newLocation, 5, CancellationToken.None);

        using var newContext = new DungeonDbContext(_options);
        var newStore = new EfPlayerStore(newContext);

        PlayerSnapshot? retrieved = await newStore.GetPlayerAsync(created.PlayerId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(newLocation.X, retrieved.Location.X);
        Assert.Equal(newLocation.Y, retrieved.Location.Y);
        Assert.Equal(5, retrieved.RoomId);
    }

    [Fact]
    public async Task DisconnectPlayer_Persists_AcrossNewContext()
    {
        PlayerSnapshot created = await _playerStore.CreatePlayerAsync(new Location(1f, 1f), CancellationToken.None);

        await _playerStore.DisconnectPlayerAsync(created.PlayerId, CancellationToken.None);

        using var newContext = new DungeonDbContext(_options);
        var newStore = new EfPlayerStore(newContext);

        PlayerSnapshot? retrieved = await newStore.GetPlayerAsync(created.PlayerId, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.False(retrieved.IsOnline);
    }

    [Fact]
    public async Task GetActivePlayerCount_ReturnsCorrectCount_AcrossNewContext()
    {
        PlayerSnapshot p1 = await _playerStore.CreatePlayerAsync(new Location(1f, 1f), CancellationToken.None);
        await _playerStore.CreatePlayerAsync(new Location(2f, 2f), CancellationToken.None);
        await _playerStore.CreatePlayerAsync(new Location(3f, 3f), CancellationToken.None);

        await _playerStore.DisconnectPlayerAsync(p1.PlayerId, CancellationToken.None);

        using var newContext = new DungeonDbContext(_options);
        var newStore = new EfPlayerStore(newContext);

        int count = await newStore.GetActivePlayerCountAsync(CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetFirstActivePlayer_ReturnsCorrectPlayer_AcrossNewContext()
    {
        PlayerSnapshot p1 = await _playerStore.CreatePlayerAsync(new Location(1f, 1f), CancellationToken.None);
        await _playerStore.CreatePlayerAsync(new Location(2f, 2f), CancellationToken.None);

        await _playerStore.DisconnectPlayerAsync(p1.PlayerId, CancellationToken.None);

        using var newContext = new DungeonDbContext(_options);
        var newStore = new EfPlayerStore(newContext);

        PlayerSnapshot? first = await newStore.GetFirstActivePlayerAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal(2f, first.Location.X);
    }
}