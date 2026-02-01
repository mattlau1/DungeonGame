using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Controllers;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using Xunit;

namespace DungeonServer.Application.Tests.Dungeon;

public static class DungeonControllerTests
{
    private record ControllerComponents(
        DungeonController Controller,
        IRoomStore RoomStore,
        IPlayerStore PlayerStore,
        RoomSubscriptionRegistry Registry);

    private static ControllerComponents CreateController()
    {
        var registry = new RoomSubscriptionRegistry();
        var roomStore = new InMemoryRoomStore(registry);
        var playerStore = new InMemoryPlayerStore();
        var movementManager = new MovementManager(playerStore);
        var architect = new DungeonArchitect(roomStore);
        var playerManager = new PlayerManager(architect, playerStore, roomStore);
        var controller = new DungeonController(playerManager, roomStore, playerStore, movementManager);
        return new ControllerComponents(controller, roomStore, playerStore, registry);
    }

    public class SpawnPlayerAsync
    {
        [Fact]
        public async Task ReturnsValidPlayerInfoResult()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.PlayerInfo.Id > 0);
            Assert.True(result.RoomId > 0);
            Assert.NotNull(result.PlayerInfo.Location);
        }

        [Fact]
        public async Task PlayerIsStoredInternally()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            PlayerInfoResult playerInfo =
                await deps.Controller.GetPlayerInfoAsync(result.PlayerInfo.Id, CancellationToken.None);

            Assert.Equal(result.PlayerInfo.Id, playerInfo.PlayerInfo.Id);
            Assert.Equal(result.RoomId, playerInfo.RoomId);
            Assert.Equal(result.PlayerInfo.Location.X, playerInfo.PlayerInfo.Location.X);
            Assert.Equal(result.PlayerInfo.Location.Y, playerInfo.PlayerInfo.Location.Y);
        }

        [Fact]
        public async Task CreatesRoomWhenNoneExists()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            Assert.Equal(result.RoomId, room.RoomId);
            Assert.Contains(result.PlayerInfo.Id, room.PlayerIds);
        }

        [Fact]
        public async Task MultipleSpawns_GenerateUniquePlayerIds()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult result1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult result2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.NotEqual(result1.PlayerInfo.Id, result2.PlayerInfo.Id);
        }

        [Fact]
        public async Task SecondPlayerJoinsSameRoom()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult result1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult result2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.Equal(result1.RoomId, result2.RoomId);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            Assert.Equal(2, room.PlayerIds.Count);
            Assert.Contains(result1.PlayerInfo.Id, room.PlayerIds);
            Assert.Contains(result2.PlayerInfo.Id, room.PlayerIds);
        }

        [Fact]
        public async Task PlayerSpawnsAtCenterOfRoom()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            float expectedX = room.Width / 2f;
            float expectedY = room.Height / 2f;

            Assert.Equal(expectedX, result.PlayerInfo.Location.X);
            Assert.Equal(expectedY, result.PlayerInfo.Location.Y);
        }

        [Fact]
        public async Task ThreePlayers_AllJoinSameRoom()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult result1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult result2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult result3 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.Equal(result1.RoomId, result2.RoomId);
            Assert.Equal(result2.RoomId, result3.RoomId);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            Assert.Equal(3, room.PlayerIds.Count);
            Assert.Contains(result1.PlayerInfo.Id, room.PlayerIds);
            Assert.Contains(result2.PlayerInfo.Id, room.PlayerIds);
            Assert.Contains(result3.PlayerInfo.Id, room.PlayerIds);
        }

        [Fact]
        public async Task RespectsCancellationToken()
        {
            ControllerComponents deps = CreateController();

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                deps.Controller.SpawnPlayerAsync(cts.Token));
        }
    }

    public class GetPlayerInfoAsync
    {
        [Fact]
        public async Task ReturnsExistingPlayerInfo()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            PlayerInfoResult info =
                await deps.Controller.GetPlayerInfoAsync(spawned.PlayerInfo.Id, CancellationToken.None);

            Assert.NotNull(info);
            Assert.Equal(spawned.PlayerInfo.Id, info.PlayerInfo.Id);
            Assert.Equal(spawned.RoomId, info.RoomId);
            Assert.Equal(spawned.PlayerInfo.Location.X, info.PlayerInfo.Location.X);
            Assert.Equal(spawned.PlayerInfo.Location.Y, info.PlayerInfo.Location.Y);
        }

        [Fact]
        public async Task ThrowsForNonExistentPlayer()
        {
            ControllerComponents deps = CreateController();

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                deps.Controller.GetPlayerInfoAsync(999, CancellationToken.None));
        }

        [Fact]
        public async Task ReflectsUpdatedLocation()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult initialInfo =
                await deps.Controller.GetPlayerInfoAsync(spawned.PlayerInfo.Id, CancellationToken.None);

            await deps.Controller.SetMovementInputAsync(spawned.PlayerInfo.Id, 1f, 0f, CancellationToken.None);

            PlayerInfoResult updatedInfo =
                await deps.Controller.GetPlayerInfoAsync(spawned.PlayerInfo.Id, CancellationToken.None);

            Assert.NotEqual(initialInfo.PlayerInfo.Location.X, updatedInfo.PlayerInfo.Location.X);
            Assert.Equal(initialInfo.PlayerInfo.Location.X + 1f, updatedInfo.PlayerInfo.Location.X);
            Assert.Equal(initialInfo.PlayerInfo.Location.Y, updatedInfo.PlayerInfo.Location.Y);
        }
    }

    public class SetMovementInputAsync
    {
        [Fact]
        public async Task ValidMovement_ReturnsOk()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            MovementInputResponse response =
                await deps.Controller.SetMovementInputAsync(spawned.PlayerInfo.Id, 1f, 0f, CancellationToken.None);

            Assert.Equal(MovementRequestStatus.Ok, response.status);
        }

        [Fact]
        public async Task UpdatesPlayerLocation()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult initialInfo =
                await deps.Controller.GetPlayerInfoAsync(spawned.PlayerInfo.Id, CancellationToken.None);

            await deps.Controller.SetMovementInputAsync(spawned.PlayerInfo.Id, 2f, 3f, CancellationToken.None);

            PlayerInfoResult updatedInfo =
                await deps.Controller.GetPlayerInfoAsync(spawned.PlayerInfo.Id, CancellationToken.None);

            Assert.Equal(initialInfo.PlayerInfo.Location.X + 2f, updatedInfo.PlayerInfo.Location.X);
            Assert.Equal(initialInfo.PlayerInfo.Location.Y + 3f, updatedInfo.PlayerInfo.Location.Y);
        }

        [Fact]
        public async Task MovementExceedingSpeedLimit_ReturnsTooFast()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            MovementInputResponse response =
                await deps.Controller.SetMovementInputAsync(spawned.PlayerInfo.Id, 100f, 0f, CancellationToken.None);

            Assert.Equal(MovementRequestStatus.TooFast, response.status);
        }

        [Fact]
        public async Task TooFastMovement_DoesNotUpdateLocation()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult initialInfo =
                await deps.Controller.GetPlayerInfoAsync(spawned.PlayerInfo.Id, CancellationToken.None);

            await deps.Controller.SetMovementInputAsync(spawned.PlayerInfo.Id, 100f, 0f, CancellationToken.None);

            PlayerInfoResult updatedInfo =
                await deps.Controller.GetPlayerInfoAsync(spawned.PlayerInfo.Id, CancellationToken.None);

            Assert.Equal(initialInfo.PlayerInfo.Location.X, updatedInfo.PlayerInfo.Location.X);
            Assert.Equal(initialInfo.PlayerInfo.Location.Y, updatedInfo.PlayerInfo.Location.Y);
        }

        [Fact]
        public async Task ThrowsForNonExistentPlayer()
        {
            ControllerComponents deps = CreateController();

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                deps.Controller.SetMovementInputAsync(999, 1f, 0f, CancellationToken.None));
        }

        [Fact]
        public async Task MultipleMovements_EachUpdatesLocation()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            await deps.Controller.SetMovementInputAsync(spawned.PlayerInfo.Id, 1f, 0f, CancellationToken.None);
            await deps.Controller.SetMovementInputAsync(spawned.PlayerInfo.Id, 1f, 0f, CancellationToken.None);
            await deps.Controller.SetMovementInputAsync(spawned.PlayerInfo.Id, 1f, 0f, CancellationToken.None);

            PlayerInfoResult info =
                await deps.Controller.GetPlayerInfoAsync(spawned.PlayerInfo.Id, CancellationToken.None);

            float initialX = spawned.PlayerInfo.Location.X;
            Assert.Equal(initialX + 3f, info.PlayerInfo.Location.X);
        }
    }

    public class SubscribeRoomAsync
    {
        [Fact]
        public async Task CanSubscribeAfterSpawning()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            Task task = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot _ in deps.Controller.SubscribeRoomAsync(spawned.PlayerInfo.Id,
                                       spawned.RoomId,
                                       cts.Token))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await task;
        }

        [Fact]
        public async Task ReceivesRoomUpdatesAfterSubscribing()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var snapshots = new List<RoomStateSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player1.PlayerInfo.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                        if (snapshots.Count >= 1)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            await deps.RoomStore.PublishRoomUpdateAsync(roomId, RoomUpdateContext.Broadcast(), CancellationToken.None);

            await Task.Delay(100);

            Assert.True(snapshots.Count > 0);
        }

        [Fact]
        public async Task MultipleSubscribers_AllReceiveUpdates()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var snapshots1 = new List<RoomStateSnapshot>();
            Task task1 = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player1.PlayerInfo.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots1.Add(snap);
                        if (snapshots1.Count >= 1)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            var snapshots2 = new List<RoomStateSnapshot>();
            Task task2 = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player2.PlayerInfo.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots2.Add(snap);
                        if (snapshots2.Count >= 1)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            await deps.RoomStore.PublishRoomUpdateAsync(roomId, RoomUpdateContext.Broadcast(), CancellationToken.None);

            await Task.Delay(100);

            Assert.Single(snapshots1);
            Assert.Single(snapshots2);
        }

        [Fact]
        public async Task PlayerMovement_UpdateReceivedByOtherPlayer()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var snapshots = new List<RoomStateSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player1.PlayerInfo.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                        if (snapshots.Count >= 1)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            await deps.Controller.SetMovementInputAsync(player2.PlayerInfo.Id, 1f, 0f, CancellationToken.None);

            await subscriptionTask;

            Assert.True(snapshots.Count > 0);
        }

        [Fact]
        public async Task Cancellation_StopsReceiving()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player.RoomId;

            await Task.Delay(100);
            var snapshot1 = new RoomStateSnapshot(roomId, RoomType.Combat, 32, 32, new());
            deps.Registry.PublishUpdate(roomId, snapshot1, RoomUpdateContext.Broadcast());

            await Task.Delay(100);

            using var cts = new CancellationTokenSource();
            var snapshots = new List<RoomStateSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player.PlayerInfo.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            var snapshot2 = new RoomStateSnapshot(roomId, RoomType.Combat, 42, 42, new());
            deps.Registry.PublishUpdate(roomId, snapshot2, RoomUpdateContext.Broadcast());

            await Task.Delay(50);
            await cts.CancelAsync();

            await Task.Delay(100);
            var snapshot3 = new RoomStateSnapshot(roomId, RoomType.Combat, 43, 43, new());
            deps.Registry.PublishUpdate(roomId, snapshot3, RoomUpdateContext.Broadcast());

            await Task.Delay(100);
            await subscriptionTask;

            Assert.True(snapshots.Count > 0);
        }

        [Fact]
        public async Task InvalidRoomId_NoEmissions()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var snapshots = new List<RoomStateSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player.PlayerInfo.Id,
                                       999, cts.Token))
                    {
                        snapshots.Add(snap);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await subscriptionTask;

            Assert.Empty(snapshots);
        }

        [Fact]
        public async Task InvalidPlayerId_NoEmissions()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var snapshots = new List<RoomStateSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(999, roomId, cts.Token))
                    {
                        snapshots.Add(snap);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await subscriptionTask;

            Assert.Empty(snapshots);
        }

        [Fact]
        public async Task ExcludedDataLeaksToLateSubscribers()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player.RoomId;

            await Task.Delay(100);
            var excludedSnapshot = new RoomStateSnapshot(roomId, RoomType.Combat, 41, 41, new());
            deps.Registry.PublishUpdate(roomId, excludedSnapshot,
                RoomUpdateContext.ExcludePlayer(player.PlayerInfo.Id));

            await Task.Delay(100);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var snapshots = new List<RoomStateSnapshot>();

            Task task = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player.PlayerInfo.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await task;

            Assert.Empty(snapshots);
        }
    }

    public class EndToEndScenarios
    {
        [Fact]
        public async Task TwoPlayersSpawnAndSeeEachOther()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var snapshots = new List<RoomStateSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player1.PlayerInfo.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                        if (snapshots.Count >= 2)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            var moveSnapshot = new RoomStateSnapshot(roomId, RoomType.Combat, 42, 42, new());
            deps.Registry.PublishUpdate(roomId, moveSnapshot, RoomUpdateContext.Broadcast());

            await Task.Delay(50);
            await deps.Controller.SetMovementInputAsync(player2.PlayerInfo.Id, 1f, 0f, CancellationToken.None);

            await subscriptionTask;

            Assert.True(snapshots.Count >= 1);
        }

        [Fact]
        public async Task PlayerMoves_OtherPlayerReceivesUpdate()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var player1Snapshots = new List<RoomStateSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomStateSnapshot snap in deps.Controller.SubscribeRoomAsync(player1.PlayerInfo.Id,
                                       roomId,
                                       cts.Token))
                    {
                        player1Snapshots.Add(snap);
                        if (player1Snapshots.Count >= 2)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            var moveSnapshot = new RoomStateSnapshot(roomId, RoomType.Combat, 43, 43, new());
            deps.Registry.PublishUpdate(roomId, moveSnapshot, RoomUpdateContext.Broadcast());

            await Task.Delay(50);
            MovementInputResponse moveResult =
                await deps.Controller.SetMovementInputAsync(player2.PlayerInfo.Id, 1f, 0f, CancellationToken.None);

            await subscriptionTask;

            Assert.Equal(MovementRequestStatus.Ok, moveResult.status);
            Assert.True(player1Snapshots.Count >= 1);
        }

        [Fact]
        public async Task ThreePlayers_AllInSameRoom()
        {
            ControllerComponents deps = CreateController();
            PlayerInfoResult player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfoResult player3 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.Equal(player1.RoomId, player2.RoomId);
            Assert.Equal(player2.RoomId, player3.RoomId);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(player1.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            Assert.Equal(3, room.PlayerIds.Count);
        }
    }
}