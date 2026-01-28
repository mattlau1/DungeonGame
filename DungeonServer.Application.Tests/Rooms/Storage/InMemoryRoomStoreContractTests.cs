using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Tests.Rooms.Storage.Contracts;

namespace DungeonServer.Application.Tests.Rooms.Storage;

public sealed class InMemoryRoomStoreContractTests : RoomStoreContractTests
{
    protected override IRoomStore CreateStore() => new InMemoryRoomStore();
}