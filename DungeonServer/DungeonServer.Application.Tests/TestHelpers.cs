using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Player.Controllers;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Storage;
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

    public record ControllerDependencies(
        DungeonController Controller,
        InMemoryRoomStore RoomStore,
        InMemoryPlayerStore PlayerStore,
        InMemoryRoomSubscriptionRegistry Registry,
        MovementManager MovementManager,
        DungeonArchitect Architect,
        PlayerManager PlayerManager);

    public static ControllerDependencies CreateControllerDependencies()
    {
        var playerStore = new InMemoryPlayerStore();
        var serviceProvider = new ServiceCollection().AddSingleton<IPlayerStore>(playerStore).BuildServiceProvider();
        var scopeFactory = new FakeScopeFactory(serviceProvider);
        var registry = new InMemoryRoomSubscriptionRegistry(playerStore);
        var roomStore = new InMemoryRoomStore(registry, playerStore);
        var movementManager = new MovementManager(playerStore, roomStore);
        var architect = new DungeonArchitect(roomStore);
        var playerManager = new PlayerManager(architect, playerStore, roomStore);
        var controller = new DungeonController(playerManager, roomStore, playerStore, movementManager);
        return new ControllerDependencies(
            controller,
            roomStore,
            playerStore,
            registry,
            movementManager,
            architect,
            playerManager);
    }
}