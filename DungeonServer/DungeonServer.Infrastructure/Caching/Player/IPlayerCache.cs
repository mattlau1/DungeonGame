using DungeonGame.Core;

namespace DungeonServer.Infrastructure.Caching.Player;

public interface IPlayerCache
{
    Task<PlayerInfo> GetOrSetAsync(int playerId, Func<Task<PlayerInfo>> factory, TimeSpan? expiry = null, CancellationToken ct = default);
    Task SetAsync(int playerId, PlayerInfo value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task InvalidateAsync(int playerId, CancellationToken ct = default);
    Task InvalidateCountAsync(CancellationToken ct = default);
}