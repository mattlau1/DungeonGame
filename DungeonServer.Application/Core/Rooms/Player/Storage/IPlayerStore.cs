using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Player.Storage;

public interface IPlayerStore
{
    Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct);

    Task<PlayerSnapshot> UpdatePlayerAsync(int playerId, Action<PlayerInfo> updateAction, CancellationToken ct);

    Task<PlayerSnapshot?> GetPlayerAsync(int playerId, CancellationToken ct);
}