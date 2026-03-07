using System.Collections.Concurrent;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Infrastructure.InMemory.Player;

public class InMemoryPlayerStore : IPlayerStore
{
    private sealed class LockablePlayerState
    {
        public PlayerInfo PlayerInfo { get; set; }
        public SemaphoreSlim Gate { get; } = new(initialCount: 1, maxCount: 1);

        public LockablePlayerState(PlayerInfo info)
        {
            PlayerInfo = info;
        }
    }

    private readonly ConcurrentDictionary<int, LockablePlayerState> _players = new();

    private int _nextPlayerId;

    public Task<PlayerSnapshot> CreatePlayerAsync(Location initialLocation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        int playerId = Interlocked.Increment(ref _nextPlayerId);
        var info = new PlayerInfo { Id = playerId, RoomId = RoomConstants.InvalidRoomId, Location = initialLocation, IsOnline = true };
        var entry = new LockablePlayerState(info);

        if (!_players.TryAdd(playerId, entry))
        {
            throw new InvalidOperationException("Failed to create player due to id collision");
        }

        return Task.FromResult(PlayerSnapshot.From(entry.PlayerInfo));
    }

    public async Task<PlayerSnapshot> UpdateLocationAsync(
        int playerId,
        Location location,
        int roomId,
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
            PlayerInfo info = player.PlayerInfo;
            info.Location = location;
            info.RoomId = roomId;
            player.PlayerInfo = info;
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

    public Task<int> GetActivePlayerCountAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_players.Values.Count(p => p.PlayerInfo.IsOnline));
    }

    public async Task<PlayerSnapshot?> GetFirstActivePlayerAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LockablePlayerState? player = _players.Values
            .Where(p => p.PlayerInfo.IsOnline)
            .OrderBy(p => p.PlayerInfo.Id)
            .FirstOrDefault();

        if (player == null) return null;

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

    public Task DisconnectPlayerAsync(int playerId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _players.TryRemove(playerId, out _);
        return Task.CompletedTask;
    }
}