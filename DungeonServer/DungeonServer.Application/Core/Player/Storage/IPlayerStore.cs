using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Player.Storage;

public interface IPlayerStore
{
    Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct);

    Task<PlayerSnapshot> UpdateLocationAsync(int playerId, Location location, int roomId, CancellationToken ct);

    Task<PlayerSnapshot?> GetPlayerAsync(int playerId, CancellationToken ct);

    Task<IEnumerable<PlayerSnapshot>> GetAllPlayersAsync(CancellationToken ct);
    
    Task DeletePlayerAsync(int playerId, CancellationToken ct);
}