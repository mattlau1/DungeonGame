using System.Collections.Concurrent;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Movement.Controllers;

public class PlayerStateManager
{
    private readonly ConcurrentDictionary<int, PlayerState> _playerStates = new();
    private readonly IPlayerStore _playerStore;

    public PlayerStateManager(IPlayerStore playerStore)
    {
        _playerStore = playerStore;
    }


    public void AddPlayerToRoom(int playerId, int roomId, Location startPosition)
    {
        var state = new PlayerState
        {
            PlayerId = playerId,
            RoomId = roomId,
            Position = startPosition,
            ViewAngle = 0,
            LastProcessedSequence = 0,
            IsOnline = true
        };

        _playerStates.TryAdd(playerId, state);
    }

    public void RemovePlayerFromRoom(int playerId)
    {
        _playerStates.TryRemove(playerId, out _);
    }

    public List<PlayerState> GetPlayersInRoom(int roomId)
    {
        return _playerStates.Values.Where(p => p.RoomId == roomId).ToList();
    }

    public PlayerState? GetPlayerState(int playerId)
    {
        _playerStates.TryGetValue(playerId, out PlayerState? state);
        return state;
    }

    public IEnumerable<int> GetActiveRoomIds()
    {
        return _playerStates.Values.Select(p => p.RoomId).Distinct();
    }

    public void UpdatePosition(int playerId, Location newPosition)
    {
        if (_playerStates.TryGetValue(playerId, out PlayerState? state))
        {
            state.Position = newPosition;
        }
    }

    public async Task SaveAllToDatabaseAsync(CancellationToken ct)
    {
        IEnumerable<PlayerUpdate> updates =
            _playerStates.Values.Select(p => new PlayerUpdate(p.PlayerId, p.Position, p.RoomId));

        await _playerStore.UpdateLocationsBatchAsync(updates, ct);
    }
}