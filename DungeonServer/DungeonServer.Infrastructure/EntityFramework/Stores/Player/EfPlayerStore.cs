using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Infrastructure.EntityFramework.Stores.Player;

public class EfPlayerStore : IPlayerStore
{
    public Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<PlayerSnapshot> UpdateLocationAsync(int playerId, Location location, int roomId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<PlayerSnapshot?> GetPlayerAsync(int playerId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<PlayerSnapshot>> GetAllPlayersAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task DeletePlayerAsync(int playerId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}