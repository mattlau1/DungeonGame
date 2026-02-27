using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.External;

public interface IDungeonController
{
    Task<PlayerInfo> SpawnPlayerAsync(CancellationToken ct);

    Task<PlayerInfo> GetPlayerInfoAsync(int playerId, CancellationToken ct);

    Task<MovementInputResponse> SetMovementInputAsync(int playerId, float inputX, float inputY, CancellationToken ct);

    IAsyncEnumerable<RoomPlayerUpdate> SubscribeRoomAsync(int playerId, int roomId, CancellationToken ct);

    Task DisconnectPlayerAsync(int playerId, CancellationToken ct);

    Task<int> GetActivePlayerCountAsync(CancellationToken ct);
    
    Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct);
}