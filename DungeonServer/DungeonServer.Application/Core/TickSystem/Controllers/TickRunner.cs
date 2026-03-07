using System.Collections.Concurrent;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.TickSystem.Contracts;
using DungeonServer.Application.Core.TickSystem.Simulation;

namespace DungeonServer.Application.Core.TickSystem.Controllers;

public class TickRunner : ITickScheduler
{
    private readonly ISimulationQueue _simulationQueue;
    private readonly PlayerStateManager _playerStateManager;
    private readonly RoomStateManager _roomStateManager;
    private readonly IRoomSubscriptionRegistry _subscriptionRegistry;
    private readonly Dictionary<Type, ISimulation> _simulations;

    private readonly ConcurrentDictionary<int, ulong> _roomTickNumbers = new();

    private CancellationTokenSource? _cts;

    private ulong _globalTickNumber;
    private const int PersistenceInterval = 64;

    public TickRunner(
        ISimulationQueue simulationQueue,
        PlayerStateManager playerStateManager,
        RoomStateManager roomStateManager,
        IRoomSubscriptionRegistry subscriptionRegistry,
        IEnumerable<ISimulation> simulationHandlers)
    {
        _simulationQueue = simulationQueue;
        _playerStateManager = playerStateManager;
        _roomStateManager = roomStateManager;
        _subscriptionRegistry = subscriptionRegistry;

        _simulations = simulationHandlers.ToDictionary(s => s.GetType());
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = TickLoop(_cts.Token);
    }

    public void Stop() => _cts?.Cancel();

    private async Task TickLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Interlocked.Increment(ref _globalTickNumber);

            foreach (int roomId in _simulationQueue.GetActiveRooms())
            {
                RoomStateSnapshot? room = await _roomStateManager.GetRoomStateAsync(roomId, ct);
                if (room == null)
                {
                    continue;
                }

                _roomTickNumbers.AddOrUpdate(roomId, 1, (_, old) => old + 1);

                foreach (var simType in _simulationQueue.GetSimulationTypesForRoom(roomId))
                {
                    var simulation = _simulations[simType];
                    await simulation.SimulateAsync(room, ct);
                }

                List<PlayerState> players = _playerStateManager.GetPlayersInRoom(roomId);
                List<PlayerSnapshot> playerUpdates = players.Select(p => new PlayerSnapshot(
                        p.PlayerId,
                        p.RoomId,
                        p.Position,
                        p.IsOnline))
                    .ToList();

                var snapshot = new RoomPlayerUpdate { Players = playerUpdates };

                await _subscriptionRegistry.PublishUpdateAsync(
                    roomId,
                    snapshot,
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