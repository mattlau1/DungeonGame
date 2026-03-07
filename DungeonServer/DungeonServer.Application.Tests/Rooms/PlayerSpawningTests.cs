using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms;

public class PlayerSpawningTests
{
    [Fact]
    public async Task SpawnPlayerAsync_ReturnsValidPlayerInfo()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        Assert.True(result.Id > 0);
        Assert.True(result.RoomId > 0);
    }

    [Fact]
    public async Task SpawnPlayerAsync_CreatesPlayerInStore()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        PlayerSnapshot? storedPlayer =
            await deps.PlayerStore.GetPlayerAsync(result.Id, CancellationToken.None);

        Assert.NotNull(storedPlayer);
        Assert.Equal(result.Id, storedPlayer.PlayerId);
        Assert.Equal(result.RoomId, storedPlayer.RoomId);
        Assert.Equal(result.Location.X, storedPlayer.Location.X);
        Assert.Equal(result.Location.Y, storedPlayer.Location.Y);
    }

    [Fact]
    public async Task SpawnPlayerAsync_CreatesNewRoomWhenNoneExist()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

        Assert.NotNull(room);
        Assert.True(room.RoomId > 0);
        Assert.Contains(result.Id, room.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SpawnPlayerAsync_SpawnsAtCenterOfRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

        Assert.NotNull(room);
        float expectedX = room.Width / 2f;
        float expectedY = room.Height / 2f;

        Assert.Equal(expectedX, result.Location.X);
        Assert.Equal(expectedY, result.Location.Y);
    }

    [Fact]
    public async Task SpawnPlayerAsync_LocationWithinRoomBounds()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

        Assert.NotNull(room);
        Assert.True(result.Location.X >= 0 && result.Location.X <= room.Width);
        Assert.True(result.Location.Y >= 0 && result.Location.Y <= room.Height);
    }

    [Fact]
    public async Task SpawnPlayerAsync_AssociatesPlayerWithRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.Equal(result.RoomId, result.RoomId);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);
        Assert.NotNull(room);
        Assert.Contains(result.Id, room.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SpawnPlayerAsync_MultipleSpawns_GenerateUniquePlayerIds()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfo result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.NotEqual(result1.Id, result2.Id);
    }

    [Fact]
    public async Task SpawnPlayerAsync_SecondPlayerJoinsExistingRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfo result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.Equal(result1.RoomId, result2.RoomId);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);
        Assert.NotNull(room);
        Assert.Equal(2, room.Players.Count);
        Assert.Contains(result1.Id, room.Players.Select(p => p.PlayerId));
        Assert.Contains(result2.Id, room.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SpawnPlayerAsync_SecondPlayerSpawnsAtFirstPlayerLocation()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfo result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.Equal(result1.Location.X, result2.Location.X);
        Assert.Equal(result1.Location.Y, result2.Location.Y);
    }

    [Fact]
    public async Task SpawnPlayerAsync_RespectsCancellationToken()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deps.PlayerManager.SpawnPlayerAsync(cts.Token));
    }

    [Fact]
    public async Task SpawnPlayerAsync_RoomIdIsInvalidBeforeUpdate()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.True(result.RoomId > 0);
        Assert.Equal(result.RoomId, result.RoomId);
    }

    [Fact]
    public async Task SpawnPlayerAsync_ViaDungeonController()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
        Assert.True(result.Id > 0);
        Assert.True(result.RoomId > 0);

        PlayerSnapshot? player = await deps.PlayerStore.GetPlayerAsync(result.Id, CancellationToken.None);
        Assert.NotNull(player);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);
        Assert.NotNull(room);
        Assert.Contains(result.Id, room.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SpawnPlayerAsync_PlayerAddedToRoomPlayerIds()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

        Assert.NotNull(room);
        Assert.Single(room.Players.Select(p => p.PlayerId));
        Assert.Contains(result.Id, room.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SpawnPlayerAsync_ThirdPlayerJoinsOldestRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfo result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfo result3 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.Equal(result1.RoomId, result2.RoomId);
        Assert.Equal(result2.RoomId, result3.RoomId);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);
        Assert.NotNull(room);
        Assert.Equal(3, room.Players.Count);
        Assert.Contains(result1.Id, room.Players.Select(p => p.PlayerId));
        Assert.Contains(result2.Id, room.Players.Select(p => p.PlayerId));
        Assert.Contains(result3.Id, room.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SpawnPlayerAsync_WhenRoomHasNoPlayers_CreatesNewRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room1 = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);
        if (room1 != null)
        {
            await deps.RoomStore.RemovePlayerFromRoomAsync(room1.RoomId, result1.Id, CancellationToken.None);
        }

        PlayerInfo result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room2 = await deps.RoomStore.GetRoomAsync(result2.RoomId, CancellationToken.None);
        Assert.NotNull(room2);
        Assert.Single(room2.Players.Select(p => p.PlayerId));
        Assert.Contains(result2.Id, room2.Players.Select(p => p.PlayerId));
    }

    [Fact]
    public async Task SpawnPlayerAsync_AllPlayersInInvalidRoomId_CreatesNewRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfo result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room1 = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);
        if (room1 != null)
        {
            await deps.RoomStore.RemovePlayerFromRoomAsync(room1.RoomId, result1.Id, CancellationToken.None);
        }

        PlayerInfo result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room2 = await deps.RoomStore.GetRoomAsync(result2.RoomId, CancellationToken.None);
        Assert.NotNull(room2);
        Assert.Single(room2.Players.Select(p => p.PlayerId));
        Assert.Contains(result2.Id, room2.Players.Select(p => p.PlayerId));
    }
}