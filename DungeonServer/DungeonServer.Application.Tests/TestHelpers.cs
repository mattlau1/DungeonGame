using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Controllers;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Application.Core.TickSystem.Contracts;
using DungeonServer.Application.Core.TickSystem.Simulation;
using DungeonServer.Infrastructure.InMemory.Player;
using DungeonServer.Infrastructure.InMemory.Rooms;
using DungeonServer.Infrastructure.Messaging.Rooms;
using Microsoft.Extensions.DependencyInjection;

namespace DungeonServer.Application.Tests;

public static class TestHelpers
{
    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public FakeScopeFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IServiceScope CreateScope()
        {
            return new FakeScope(_serviceProvider);
        }

        private sealed class FakeScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; }

            public FakeScope(IServiceProvider serviceProvider)
            {
                ServiceProvider = serviceProvider;
            }

            public void Dispose()
            {
            }
        }
    }

    private sealed class MockTickScheduler : ITickScheduler
    {
        public void Start() { }
        public void Stop() { }
    }

    public record ControllerDependencies(
        DungeonController Controller,
        InMemoryRoomStore RoomStore,
        InMemoryPlayerStore PlayerStore,
        InMemoryRoomSubscriptionRegistry Registry,
        MovementManager MovementManager,
        DungeonArchitect Architect,
        PlayerManager PlayerManager,
        PlayerInputManager PlayerInputManager,
        PlayerStateManager PlayerStateManager);

    public static ControllerDependencies CreateControllerDependencies()
    {
        var playerStore = new InMemoryPlayerStore();
        var serviceProvider = new ServiceCollection().AddSingleton<IPlayerStore>(playerStore).BuildServiceProvider();
        var scopeFactory = new FakeScopeFactory(serviceProvider);
        var registry = new InMemoryRoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(registry, playerStore);
        var tickScheduler = new MockTickScheduler();
        var movementManager = new MovementManager(roomStore);
        var playerInputManager = new PlayerInputManager();
        var playerStateManager = new PlayerStateManager(playerStore);
        var simulationQueue = new SimulationQueue();
        var architect = new DungeonArchitect(roomStore);
        var playerManager = new PlayerManager(architect, playerStore, roomStore, playerStateManager, simulationQueue);
        var controller = new DungeonController(playerManager, roomStore, playerStore, playerInputManager, movementManager);
        return new ControllerDependencies(
            controller,
            roomStore,
            playerStore,
            registry,
            movementManager,
            architect,
            playerManager,
            playerInputManager,
            playerStateManager);
    }

    public static InputCommand CreateInputCommand(int playerId, float moveX, float moveY, uint sequence)
    {
        return new InputCommand
        {
            PlayerId = playerId,
            Sequence = sequence,
            ClientTimestamp = 0,
            Input = new MovementInput { MoveX = moveX, MoveY = moveY }
        };
    }

    public static PlayerState CreatePlayerState(int playerId, int roomId, float x, float y)
    {
        return new PlayerState
        {
            PlayerId = playerId,
            RoomId = roomId,
            Position = new Location(x, y),
            ViewAngle = 0,
            LastProcessedSequence = 0,
            IsOnline = true
        };
    }

    public static async Task<PlayerState> SimulateTickForPlayer(
        this ControllerDependencies deps,
        int playerId,
        int roomId,
        CancellationToken ct = default)
    {
        var room = await deps.RoomStore.GetRoomAsync(roomId, ct);
        var playerState = deps.PlayerStateManager.GetPlayerState(playerId);
        
        if (playerState == null)
        {
            throw new InvalidOperationException($"Player {playerId} not found in PlayerStateManager");
        }

        var commands = deps.PlayerInputManager.DequeueAllForPlayer(playerId);
        if (commands.Count > 0)
        {
            await deps.MovementManager.SimulatePhysics(playerState, commands, room, ct);
            playerState.LastProcessedSequence = commands[^1].Sequence;
        }

        return playerState;
    }
}
