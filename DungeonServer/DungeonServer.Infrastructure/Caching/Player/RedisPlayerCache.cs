using DungeonServer.Infrastructure.Caching.Generic;
using PlayerInfo = DungeonGame.Core.PlayerInfo;

namespace DungeonServer.Infrastructure.Caching.Player;

public class RedisPlayerCache : IPlayerCache
{
    private readonly IProtoCacheService _cache;

    public RedisPlayerCache(IProtoCacheService cache)
    {
        _cache = cache;
    }

    public async Task<PlayerInfo> GetOrSetAsync(
        int playerId,
        Func<Task<PlayerInfo>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            PlayerCacheKeys.Player(playerId),
            factory,
            PlayerInfo.Parser,
            expiry ?? TimeSpan.FromSeconds(30),
            ct);
    }

    public async Task SetAsync(int playerId, PlayerInfo value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        await _cache.SetAsync(
            PlayerCacheKeys.Player(playerId),
            value,
            expiry ?? TimeSpan.FromSeconds(30),
            ct);
    }

    public async Task InvalidateAsync(int playerId, CancellationToken ct = default)
    {
        await _cache.DeleteAsync(PlayerCacheKeys.Player(playerId), ct);
    }

    public async Task InvalidateCountAsync(CancellationToken ct = default)
    {
        await _cache.DeleteAsync(PlayerCacheKeys.Count, ct);
        await _cache.DeleteAsync(PlayerCacheKeys.FirstActive, ct);
    }
}