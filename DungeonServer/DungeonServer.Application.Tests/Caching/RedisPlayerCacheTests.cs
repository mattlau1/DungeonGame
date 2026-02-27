using DungeonServer.Infrastructure.Caching.Generic;
using DungeonServer.Infrastructure.Caching.Player;
using Moq;
using Xunit;
using PlayerInfo = DungeonGame.Core.PlayerInfo;

namespace DungeonServer.Application.Tests.Caching;

public class RedisPlayerCacheTests
{
    private readonly Mock<IProtoCacheService> _mockProtoCache;
    private readonly RedisPlayerCache _cache;

    public RedisPlayerCacheTests()
    {
        _mockProtoCache = new Mock<IProtoCacheService>();
        _cache = new RedisPlayerCache(_mockProtoCache.Object);
    }

    [Fact]
    public async Task GetOrSetAsync_CallsProtoCache_WithCorrectKey()
    {
        var factory = () => Task.FromResult(new PlayerInfo { Id = 1, RoomId = 1 });
        _mockProtoCache.Setup(x => x.GetOrSetAsync(
                "player:123",
                It.IsAny<Func<Task<PlayerInfo>>>(),
                PlayerInfo.Parser,
                TimeSpan.FromSeconds(30),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerInfo { Id = 1, RoomId = 1 });

        // Act
        await _cache.GetOrSetAsync(123, factory, TimeSpan.FromSeconds(30), CancellationToken.None);

        // Assert
        _mockProtoCache.Verify(
            x => x.GetOrSetAsync(
                "player:123",
                It.IsAny<Func<Task<PlayerInfo>>>(),
                PlayerInfo.Parser,
                TimeSpan.FromSeconds(30),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_CallsProtoCache_WithCorrectKey()
    {
        var playerInfo = new PlayerInfo { Id = 1, RoomId = 1 };

        await _cache.SetAsync(1, playerInfo, TimeSpan.FromSeconds(30), CancellationToken.None);

        _mockProtoCache.Verify(
            x => x.SetAsync("player:1", playerInfo, TimeSpan.FromSeconds(30), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateAsync_CallsDelete_WithCorrectKey()
    {
        await _cache.InvalidateAsync(123, CancellationToken.None);

        _mockProtoCache.Verify(x => x.DeleteAsync("player:123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateCountAsync_CallsDelete_ForCountAndFirstActive()
    {
        await _cache.InvalidateCountAsync(CancellationToken.None);

        _mockProtoCache.Verify(x => x.DeleteAsync("player:count", It.IsAny<CancellationToken>()), Times.Once);
        _mockProtoCache.Verify(x => x.DeleteAsync("player:first-active", It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class CacheKeysTests
{
    [Fact]
    public void PlayerCacheKeys_Player_FormatsCorrectly()
    {
        Assert.Equal("player:123", PlayerCacheKeys.Player(123));
    }

    [Fact]
    public void PlayerCacheKeys_Count_IsCorrect()
    {
        Assert.Equal("player:count", PlayerCacheKeys.Count);
    }

    [Fact]
    public void PlayerCacheKeys_FirstActive_IsCorrect()
    {
        Assert.Equal("player:first-active", PlayerCacheKeys.FirstActive);
    }
}