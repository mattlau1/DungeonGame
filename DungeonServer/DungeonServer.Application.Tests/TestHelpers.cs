using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Player.Controllers;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Storage;

namespace DungeonServer.Application.Tests;

public static class TestHelpers
{
    public record ControllerDependencies(
        DungeonController Controller,
        InMemoryRoomStore RoomStore,
        InMemoryPlayerStore PlayerStore,
        RoomSubscriptionRegistry Registry,
        MovementManager MovementManager,
        DungeonArchitect Architect,
        PlayerManager PlayerManager);

    public static ControllerDependencies CreateControllerDependencies()
    {
        var playerStore = new InMemoryPlayerStore();
        var registry = new RoomSubscriptionRegistry(playerStore);
        var roomStore = new InMemoryRoomStore(registry);
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
