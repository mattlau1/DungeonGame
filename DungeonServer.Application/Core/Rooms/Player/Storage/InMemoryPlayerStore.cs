using System.Collections.Concurrent;
using DungeonServer.Application.Core.Rooms.Player.Models;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Rooms.Player.Storage;

public class InMemoryPlayerStore : IPlayerStore
{
    private sealed class LockablePlayerState
    {
        public PlayerInfo PlayerInfo { get; }
        public SemaphoreSlim Gate { get; } = new(initialCount: 1, maxCount: 1);

        public LockablePlayerState(int playerId, PlayerInfo info)
        {
            PlayerInfo = info;
            PlayerInfo.Id = playerId;
        }
    }

    private readonly ConcurrentDictionary<int, LockablePlayerState> _players = new();

    private int _nextPlayerId;

    public Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        int playerId = Interlocked.Increment(ref _nextPlayerId);
        var info = new PlayerInfo { Id = playerId, RoomId = 0, Location = initialLocation };
        var entry = new LockablePlayerState(playerId, info);

        if (!_players.TryAdd(playerId, entry))
        {
            throw new InvalidOperationException("Failed to create player due to id collision");
        }

        return Task.FromResult(PlayerSnapshot.From(entry.PlayerInfo));
    }

    public async Task<PlayerSnapshot> UpdatePlayerAsync(
        int playerId,
        Action<PlayerInfo> updateAction,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_players.TryGetValue(playerId, out LockablePlayerState? player))
        {
            throw new KeyNotFoundException($"Player Id {playerId} does not exist.");
        }

        await player.Gate.WaitAsync(ct);
        try
        {
            updateAction(player.PlayerInfo);
            return PlayerSnapshot.From(player.PlayerInfo);
        }
        finally
        {
            player.Gate.Release();
        }
    }

    public async Task<PlayerSnapshot?> GetPlayerAsync(int playerId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_players.TryGetValue(playerId, out LockablePlayerState? player))
        {
            return null;
        }

        await player.Gate.WaitAsync(ct);
        try
        {
            return PlayerSnapshot.From(player.PlayerInfo);
        }
        finally
        {
            player.Gate.Release();
        }
    }

    public Task<IEnumerable<PlayerSnapshot>> GetAllPlayersAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_players.Values.Select(p => PlayerSnapshot.From(p.PlayerInfo)));
    }
}