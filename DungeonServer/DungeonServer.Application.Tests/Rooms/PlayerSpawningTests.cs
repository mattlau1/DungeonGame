using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Controllers;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms;

public class PlayerSpawningTests
{
    [Fact]
    public async Task SpawnPlayerAsync_ReturnsValidPlayerInfoResult()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.PlayerInfo.Id > 0);
        Assert.True(result.RoomId > 0);
        Assert.NotNull(result.PlayerInfo.Location);
    }

    [Fact]
    public async Task SpawnPlayerAsync_CreatesPlayerInStore()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        PlayerSnapshot? storedPlayer =
            await deps.PlayerStore.GetPlayerAsync(result.PlayerInfo.Id, CancellationToken.None);

        Assert.NotNull(storedPlayer);
        Assert.Equal(result.PlayerInfo.Id, storedPlayer.PlayerId);
        Assert.Equal(result.RoomId, storedPlayer.RoomId);
        Assert.Equal(result.PlayerInfo.Location.X, storedPlayer.Location.X);
        Assert.Equal(result.PlayerInfo.Location.Y, storedPlayer.Location.Y);
    }

    [Fact]
    public async Task SpawnPlayerAsync_CreatesNewRoomWhenNoneExist()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

        Assert.NotNull(room);
        Assert.True(room.RoomId > 0);
        Assert.Contains(result.PlayerInfo.Id, room.PlayerIds);
    }

    [Fact]
    public async Task SpawnPlayerAsync_SpawnsAtCenterOfRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

        Assert.NotNull(room);
        float expectedX = room.Width / 2f;
        float expectedY = room.Height / 2f;

        Assert.Equal(expectedX, result.PlayerInfo.Location.X);
        Assert.Equal(expectedY, result.PlayerInfo.Location.Y);
    }

    [Fact]
    public async Task SpawnPlayerAsync_LocationWithinRoomBounds()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

        Assert.NotNull(room);
        Assert.True(result.PlayerInfo.Location.X >= 0 && result.PlayerInfo.Location.X <= room.Width);
        Assert.True(result.PlayerInfo.Location.Y >= 0 && result.PlayerInfo.Location.Y <= room.Height);
    }

    [Fact]
    public async Task SpawnPlayerAsync_AssociatesPlayerWithRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.Equal(result.RoomId, result.PlayerInfo.RoomId);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);
        Assert.NotNull(room);
        Assert.Contains(result.PlayerInfo.Id, room.PlayerIds);
    }

    [Fact]
    public async Task SpawnPlayerAsync_MultipleSpawns_GenerateUniquePlayerIds()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfoResult result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.NotEqual(result1.PlayerInfo.Id, result2.PlayerInfo.Id);
    }

    [Fact]
    public async Task SpawnPlayerAsync_SecondPlayerJoinsExistingRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfoResult result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.Equal(result1.RoomId, result2.RoomId);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);
        Assert.NotNull(room);
        Assert.Equal(2, room.PlayerIds.Count);
        Assert.Contains(result1.PlayerInfo.Id, room.PlayerIds);
        Assert.Contains(result2.PlayerInfo.Id, room.PlayerIds);
    }

    [Fact]
    public async Task SpawnPlayerAsync_SecondPlayerSpawnsAtFirstPlayerLocation()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfoResult result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.Equal(result1.PlayerInfo.Location.X, result2.PlayerInfo.Location.X);
        Assert.Equal(result1.PlayerInfo.Location.Y, result2.PlayerInfo.Location.Y);
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
        PlayerInfoResult result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.True(result.RoomId > 0);
        Assert.Equal(result.RoomId, result.PlayerInfo.RoomId);
    }

    [Fact]
    public async Task SpawnPlayerAsync_ViaDungeonController()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.PlayerInfo.Id > 0);
        Assert.True(result.RoomId > 0);
        Assert.NotNull(result.PlayerInfo.Location);

        PlayerSnapshot? player = await deps.PlayerStore.GetPlayerAsync(result.PlayerInfo.Id, CancellationToken.None);
        Assert.NotNull(player);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);
        Assert.NotNull(room);
        Assert.Contains(result.PlayerInfo.Id, room.PlayerIds);
    }

    [Fact]
    public async Task SpawnPlayerAsync_PlayerAddedToRoomPlayerIds()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

        Assert.NotNull(room);
        Assert.Single(room.PlayerIds);
        Assert.Contains(result.PlayerInfo.Id, room.PlayerIds);
    }

    [Fact]
    public async Task SpawnPlayerAsync_ThirdPlayerJoinsOldestRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfoResult result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        PlayerInfoResult result3 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        Assert.Equal(result1.RoomId, result2.RoomId);
        Assert.Equal(result2.RoomId, result3.RoomId);

        RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);
        Assert.NotNull(room);
        Assert.Equal(3, room.PlayerIds.Count);
        Assert.Contains(result1.PlayerInfo.Id, room.PlayerIds);
        Assert.Contains(result2.PlayerInfo.Id, room.PlayerIds);
        Assert.Contains(result3.PlayerInfo.Id, room.PlayerIds);
    }

    [Fact]
    public async Task SpawnPlayerAsync_WhenRoomHasNoPlayers_CreatesNewRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);
        IEnumerable<PlayerSnapshot> allPlayers = await deps.PlayerStore.GetAllPlayersAsync(CancellationToken.None);

        foreach (PlayerSnapshot player in allPlayers)
        {
            await deps.PlayerStore.UpdatePlayerAsync(player.PlayerId, p => p.RoomId = 0, CancellationToken.None);
        }

        RoomStateSnapshot? room1 = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);
        if (room1 != null)
        {
            await deps.RoomStore.UpdateRoomAsync(
                room1.RoomId,
                r => r.PlayerIds.Clear(),
                RoomUpdateContext.Broadcast(),
                CancellationToken.None);
        }

        PlayerInfoResult result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room2 = await deps.RoomStore.GetRoomAsync(result2.RoomId, CancellationToken.None);
        Assert.NotNull(room2);
        Assert.Single(room2.PlayerIds);
        Assert.Contains(result2.PlayerInfo.Id, room2.PlayerIds);
    }

    [Fact]
    public async Task SpawnPlayerAsync_AllPlayersInInvalidRoomId_CreatesNewRoom()
    {
        TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
        PlayerInfoResult result1 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        await deps.PlayerStore.UpdatePlayerAsync(result1.PlayerInfo.Id, p => p.RoomId = 0, CancellationToken.None);
        RoomStateSnapshot? room1 = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);
        if (room1 != null)
        {
            await deps.RoomStore.UpdateRoomAsync(
                room1.RoomId,
                r => r.PlayerIds.Remove(result1.PlayerInfo.Id),
                RoomUpdateContext.Broadcast(),
                CancellationToken.None);
        }

        PlayerInfoResult result2 = await deps.PlayerManager.SpawnPlayerAsync(CancellationToken.None);

        RoomStateSnapshot? room2 = await deps.RoomStore.GetRoomAsync(result2.RoomId, CancellationToken.None);
        Assert.NotNull(room2);
        Assert.Single(room2.PlayerIds);
        Assert.Contains(result2.PlayerInfo.Id, room2.PlayerIds);
    }
}