using System.Collections.Concurrent;
using System.Collections.Immutable;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.TickSystem.Contracts;

namespace DungeonServer.Application.Core.TickSystem.Controllers;

public class TickRunner : ITickScheduler
{
    private readonly IPlayerInputManager _playerInputManager;
    private readonly PlayerStateManager _playerStateManager;

    private readonly RoomStateManager _roomStateManager;

    private readonly IMovementManager _movementManager;
    private readonly IRoomSubscriptionRegistry _subscriptionRegistry;

    private ImmutableList<int> _roomIdsToTick = ImmutableList<int>.Empty;
    private readonly ConcurrentDictionary<int, ulong> _roomTickNumbers = new(); // Room id -> Tick number

    private CancellationTokenSource? _cts;

    private ulong _globalTickNumber;
    private const int PersistenceInterval = 64;

    public TickRunner(
        IPlayerInputManager playerInputManager,
        PlayerStateManager playerStateManager,
        IMovementManager movementManager,
        IRoomSubscriptionRegistry subscriptionRegistry,
        RoomStateManager roomStateManager)
    {
        _playerInputManager = playerInputManager;
        _playerStateManager = playerStateManager;
        _movementManager = movementManager;
        _subscriptionRegistry = subscriptionRegistry;
        _roomStateManager = roomStateManager;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = TickLoop(_cts.Token);
    }

    public void Stop() => _cts?.Cancel();

    public void RegisterRoom(int roomId)
    {
        Interlocked.Exchange(ref _roomIdsToTick, _roomIdsToTick.Add(roomId));
        _roomTickNumbers.TryAdd(roomId, 0);
    }

    public void UnregisterRoom(int roomId)
    {
        Interlocked.Exchange(ref _roomIdsToTick, _roomIdsToTick.Remove(roomId));
        _roomTickNumbers.TryRemove(roomId, out _);
    }

    /// <summary>
    /// Process all rooms → Physics → Snapshots
    /// </summary>
    private async Task TickLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Interlocked.Increment(ref _globalTickNumber);

            foreach (int roomId in _roomIdsToTick)
            {
                RoomStateSnapshot? room = await _roomStateManager.GetRoomStateAsync(roomId, CancellationToken.None);

                _roomTickNumbers.AddOrUpdate(roomId, 1, (_, old) => old + 1);

                List<PlayerState> players = _playerStateManager.GetPlayersInRoom(roomId);
                foreach (PlayerState player in players)
                {
                    List<InputCommand> cmds = _playerInputManager.DequeueAllForPlayer(player.PlayerId);
                    if (cmds.Count > 0)
                    {
                        await _movementManager.SimulatePhysics(player, cmds, room, ct);
                        player.LastProcessedSequence = cmds[^1].Sequence;
                    }
                }

                List<PlayerSnapshot> playerUpdates = players.Select(p => new PlayerSnapshot(
                        p.PlayerId,
                        p.RoomId,
                        p.Position,
                        p.IsOnline))
                    .ToList();

                var snapshot = new RoomPlayerUpdate { RoomId = roomId, Players = playerUpdates };

                await _subscriptionRegistry.PublishUpdateAsync(
                    roomId,
                    snapshot,
                    RoomUpdateContext.Broadcast(),
                    CancellationToken.None);
            }

            if (_globalTickNumber % PersistenceInterval == 0)
            {
                await _playerStateManager.SaveAllToDatabaseAsync(CancellationToken.None);
            }

            await Task.Delay(15, ct);
        }
    }
}