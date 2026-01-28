using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms.Storage.Contracts;

public abstract class RoomStoreContractTests
{
    protected abstract IRoomStore CreateStore();

    private static RoomState GenerateNewRoom()
    {
        return new RoomState(RoomType.Combat, 10, 8);
    }

    [Fact]
    public async Task Create_AssignsId_AndReturnsSnapshot()
    {
        IRoomStore store = CreateStore();

        RoomState room = GenerateNewRoom();
        RoomStateSnapshot snapshot = await store.CreateRoomAsync(room, CancellationToken.None);

        Assert.True(snapshot.RoomId > 0);
        Assert.Equal(RoomType.Combat, snapshot.RoomType);
        Assert.Equal(room.Width, snapshot.Width);
        Assert.Equal(room.Height, snapshot.Height);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenMissing()
    {
        IRoomStore store = CreateStore();

        RoomStateSnapshot? snapshot = await store.GetRoomAsync(999, CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Update_Throws_WhenMissing()
    {
        IRoomStore store = CreateStore();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.UpdateRoomAsync(999, _ => { }, CancellationToken.None));
    }

    [Fact]
    public async Task Update_MutatesState_AndReturnsSnapshot()
    {
        IRoomStore store = CreateStore();
        
        RoomStateSnapshot created = await store.CreateRoomAsync(GenerateNewRoom(), CancellationToken.None);

        RoomStateSnapshot updated = await store.UpdateRoomAsync(created.RoomId, s =>
        {
            s.Width = 42;
            s.RoomType = RoomType.Boss;
        }, CancellationToken.None);

        Assert.Equal(created.RoomId, updated.RoomId);
        Assert.Equal(42, updated.Width);
        Assert.Equal(RoomType.Boss, updated.RoomType);
    }
}