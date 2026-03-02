using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<int, ulong> _roomTickNumbers = new(); // Room id -> Tick number

    private CancellationTokenSource? _cts;

    private ulong _globalTickNumber;
    private const int PersistenceInterval = 64;

    public TickRunner(
        IPlayerInputManager playerInputManager,
        PlayerStateManager playerStateManager,
        RoomStateManager roomStateManager,
        IMovementManager movementManager,
        IRoomSubscriptionRegistry subscriptionRegistry)
    {
        _playerInputManager = playerInputManager;
        _playerStateManager = playerStateManager;
        _roomStateManager = roomStateManager;
        _movementManager = movementManager;
        _subscriptionRegistry = subscriptionRegistry;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = TickLoop(_cts.Token);
    }

    public void Stop() => _cts?.Cancel();

    /// <summary>
    /// Process all rooms → Physics → Snapshots
    /// </summary>
    private async Task TickLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Interlocked.Increment(ref _globalTickNumber);

            IEnumerable<int> activeRoomIds = _playerStateManager.GetActiveRoomIds();

            foreach (int roomId in activeRoomIds)
            {
                RoomStateSnapshot? room = await _roomStateManager.GetRoomStateAsync(roomId, ct);
                if (room == null)
                {
                    continue;
                }

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