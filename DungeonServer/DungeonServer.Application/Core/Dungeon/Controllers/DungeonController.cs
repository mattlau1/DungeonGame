using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Application.External;

namespace DungeonServer.Application.Core.Dungeon.Controllers;

public sealed class DungeonController : IDungeonController
{
    private readonly IPlayerManager _playerManager;
    private readonly IRoomStore _roomStore;
    private readonly IPlayerStore _playerStore;
    private readonly IMovementManager _movementManager;

    public DungeonController(
        IPlayerManager playerManager,
        IRoomStore roomStore,
        IPlayerStore playerStore,
        IMovementManager movementManager)
    {
        _playerManager = playerManager;
        _roomStore = roomStore;
        _playerStore = playerStore;
        _movementManager = movementManager;
    }

    public async Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct)
    {
        return await _playerManager.SpawnPlayerAsync(ct);
    }

    public async Task<PlayerInfoResult> GetPlayerInfoAsync(int playerId, CancellationToken ct)
    {
        PlayerSnapshot? player = await _playerStore.GetPlayerAsync(playerId, ct);
        if (player == null)
        {
            throw new KeyNotFoundException($"Player Id {playerId} does not exist.");
        }

        var playerInfo = new PlayerInfo { Id = player.PlayerId, RoomId = player.RoomId, Location = player.Location };

        return new PlayerInfoResult(player.RoomId, playerInfo);
    }

    public async Task<MovementInputResponse> SetMovementInputAsync(
        int playerId,
        float inputX,
        float inputY,
        CancellationToken ct)
    {
        PlayerInfoResult currPlayer = await GetPlayerInfoAsync(playerId, ct);
        Location currLocation = currPlayer.PlayerInfo.Location;

        var destination = new Location(currLocation.X + inputX, currLocation.Y + inputY);
        var moveRequest = new MovementInputRequest(playerId, destination);

        return await _movementManager.SetMovementInput(moveRequest, ct);
    }

    public IAsyncEnumerable<RoomStateSnapshot> SubscribeRoomAsync(int playerId, int roomId, CancellationToken ct)
    {
        return _roomStore.SubscribeRoomAsync(playerId, roomId, ct);
    }
}