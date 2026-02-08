using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Player.Storage;

namespace DungeonServer.Application.Tests.Rooms;

public sealed class InMemoryRoomStoreContractTests : RoomStoreContractTests
{
    protected override IRoomStore CreateStore() =>
        new InMemoryRoomStore(new RoomSubscriptionRegistry(new InMemoryPlayerStore()));
}