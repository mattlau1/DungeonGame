using DungeonGame.Core;

namespace DungeonServer.Infrastructure.Caching.Player;

public class NoOpPlayerCache : IPlayerCache
{
    public Task<PlayerInfo> GetOrSetAsync(int playerId, Func<Task<PlayerInfo>> factory, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        return factory();
    }

    public Task SetAsync(int playerId, PlayerInfo value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(int playerId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task InvalidateCountAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}