using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.External;

namespace DungeonServer.Application.Core.Dungeon.Controllers;

public sealed class DungeonController : IDungeonController
{
    private readonly IPlayerManager _playerManager;
    private readonly IRoomStore _roomStore;
    private readonly IPlayerStore _playerStore;
    private readonly IPlayerInputManager _playerInputManager;

    public DungeonController(
        IPlayerManager playerManager,
        IRoomStore roomStore,
        IPlayerStore playerStore,
        IPlayerInputManager playerInputManager)
    {
        _playerManager = playerManager;
        _roomStore = roomStore;
        _playerStore = playerStore;
        _playerInputManager = playerInputManager;
    }

    public async Task<PlayerInfo> SpawnPlayerAsync(CancellationToken ct)
    {
        return await _playerManager.SpawnPlayerAsync(ct);
    }

    public async Task<PlayerInfo> GetPlayerInfoAsync(int playerId, CancellationToken ct)
    {
        PlayerSnapshot? player = await _playerStore.GetPlayerAsync(playerId, ct);
        if (player == null)
        {
            throw new KeyNotFoundException($"Player Id {playerId} does not exist.");
        }

        return player.ToPlayerInfo();
    }

    public Task SendInputCommandAsync(InputCommand command, CancellationToken ct)
    {
        _playerInputManager.EnqueueCommand(command);
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<ReadOnlyMemory<byte>> SubscribeRoomAsync(int playerId, int roomId, CancellationToken ct)
    {
        return _roomStore.SubscribeRoomAsync(playerId, roomId, ct);
    }

    public async Task DisconnectPlayerAsync(int playerId, CancellationToken ct)
    {
        await _playerManager.DisconnectPlayerAsync(playerId, ct);
    }

    public async Task<int> GetActivePlayerCountAsync(CancellationToken ct)
    {
        return await _playerStore.GetActivePlayerCountAsync(ct);
    }

    public async Task<RoomStateSnapshot?> GetRoomAsync(int roomId, CancellationToken ct)
    {
        return await _roomStore.GetRoomAsync(roomId, ct);
    }
}