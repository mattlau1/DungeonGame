using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Player.Storage;

public interface IPlayerStore
{
    Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct);

    Task<PlayerSnapshot> UpdateLocationAsync(int playerId, Location location, int roomId, CancellationToken ct);

    Task<PlayerSnapshot?> GetPlayerAsync(int playerId, CancellationToken ct);

    Task<int> GetActivePlayerCountAsync(CancellationToken ct);
    
    Task<PlayerSnapshot?> GetFirstActivePlayerAsync(CancellationToken ct);
    
    Task DisconnectPlayerAsync(int playerId, CancellationToken ct);
}