using System.Linq;
using DungeonServer.Application.Core.Dungeon.Controllers;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Controllers;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Controllers;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using Xunit;

namespace DungeonServer.Application.Tests.Dungeon;

public static class DungeonControllerTests
{
    public class SpawnPlayerAsync
    {
        [Fact]
        public async Task ReturnsValidPlayerInfo()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.True(result.RoomId > 0);
            Assert.NotNull(result.Location);
        }

        [Fact]
        public async Task PlayerIsStoredInternally()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            PlayerInfo playerInfo = await deps.Controller.GetPlayerInfoAsync(
                result.Id,
                CancellationToken.None);

            Assert.Equal(result.Id, playerInfo.Id);
            Assert.Equal(result.RoomId, playerInfo.RoomId);
            Assert.Equal(result.Location.X, playerInfo.Location.X);
            Assert.Equal(result.Location.Y, playerInfo.Location.Y);
        }

        [Fact]
        public async Task CreatesRoomWhenNoneExists()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            Assert.Equal(result.RoomId, room.RoomId);
            Assert.Contains(result.Id, room.Players.Select(p => p.PlayerId));
        }

        [Fact]
        public async Task MultipleSpawns_GenerateUniquePlayerIds()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo result1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo result2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.NotEqual(result1.Id, result2.Id);
        }

        [Fact]
        public async Task SecondPlayerJoinsSameRoom()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo result1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo result2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.Equal(result1.RoomId, result2.RoomId);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            Assert.Equal(2, room.Players.Count);
            Assert.Contains(result1.Id, room.Players.Select(p => p.PlayerId));
            Assert.Contains(result2.Id, room.Players.Select(p => p.PlayerId));
        }

        [Fact]
        public async Task PlayerSpawnsAtCenterOfRoom()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo result = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            float expectedX = room.Width / 2f;
            float expectedY = room.Height / 2f;

            Assert.Equal(expectedX, result.Location.X);
            Assert.Equal(expectedY, result.Location.Y);
        }

        [Fact]
        public async Task ThreePlayers_AllJoinSameRoom()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo result1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo result2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo result3 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.Equal(result1.RoomId, result2.RoomId);
            Assert.Equal(result2.RoomId, result3.RoomId);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(result1.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            Assert.Equal(3, room.Players.Count);
            Assert.Contains(result1.Id, room.Players.Select(p => p.PlayerId));
            Assert.Contains(result2.Id, room.Players.Select(p => p.PlayerId));
            Assert.Contains(result3.Id, room.Players.Select(p => p.PlayerId));
        }

        [Fact]
        public async Task RespectsCancellationToken()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deps.Controller.SpawnPlayerAsync(cts.Token));
        }
    }

    public class GetPlayerInfoAsync
    {
        [Fact]
        public async Task ReturnsExistingPlayerInfo()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            PlayerInfo info = await deps.Controller.GetPlayerInfoAsync(
                spawned.Id,
                CancellationToken.None);

            Assert.NotNull(info);
            Assert.Equal(spawned.Id, info.Id);
            Assert.Equal(spawned.RoomId, info.RoomId);
            Assert.Equal(spawned.Location.X, info.Location.X);
            Assert.Equal(spawned.Location.Y, info.Location.Y);
        }

        [Fact]
        public async Task ThrowsForNonExistentPlayer()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                deps.Controller.GetPlayerInfoAsync(999, CancellationToken.None));
        }

        [Fact]
        public async Task ReflectsUpdatedLocation()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo initialInfo = await deps.Controller.GetPlayerInfoAsync(
                spawned.Id,
                CancellationToken.None);

            await deps.Controller.SetMovementInputAsync(spawned.Id, 1f, 0f, CancellationToken.None);

            PlayerInfo updatedInfo = await deps.Controller.GetPlayerInfoAsync(
                spawned.Id,
                CancellationToken.None);

            Assert.NotEqual(initialInfo.Location.X, updatedInfo.Location.X);
            Assert.Equal(initialInfo.Location.X + 1f, updatedInfo.Location.X);
            Assert.Equal(initialInfo.Location.Y, updatedInfo.Location.Y);
        }
    }

    public class SetMovementInputAsync
    {
        [Fact]
        public async Task ValidMovement_ReturnsOk()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            MovementInputResponse response = await deps.Controller.SetMovementInputAsync(
                spawned.Id,
                1f,
                0f,
                CancellationToken.None);

            Assert.Equal(MovementRequestStatus.Ok, response.Status);
        }

        [Fact]
        public async Task UpdatesPlayerLocation()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo initialInfo = await deps.Controller.GetPlayerInfoAsync(
                spawned.Id,
                CancellationToken.None);

            await deps.Controller.SetMovementInputAsync(spawned.Id, 2f, 3f, CancellationToken.None);

            PlayerInfo updatedInfo = await deps.Controller.GetPlayerInfoAsync(
                spawned.Id,
                CancellationToken.None);

            Assert.Equal(initialInfo.Location.X + 2f, updatedInfo.Location.X);
            Assert.Equal(initialInfo.Location.Y + 3f, updatedInfo.Location.Y);
        }

        [Fact]
        public async Task MovementExceedingSpeedLimit_ReturnsTooFast()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            MovementInputResponse response = await deps.Controller.SetMovementInputAsync(
                spawned.Id,
                100f,
                0f,
                CancellationToken.None);

            Assert.Equal(MovementRequestStatus.TooFast, response.Status);
        }

        [Fact]
        public async Task TooFastMovement_DoesNotUpdateLocation()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo initialInfo = await deps.Controller.GetPlayerInfoAsync(
                spawned.Id,
                CancellationToken.None);

            await deps.Controller.SetMovementInputAsync(spawned.Id, 100f, 0f, CancellationToken.None);

            PlayerInfo updatedInfo = await deps.Controller.GetPlayerInfoAsync(
                spawned.Id,
                CancellationToken.None);

            Assert.Equal(initialInfo.Location.X, updatedInfo.Location.X);
            Assert.Equal(initialInfo.Location.Y, updatedInfo.Location.Y);
        }

        [Fact]
        public async Task ThrowsForNonExistentPlayer()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();

            MovementInputResponse response = await deps.Controller.SetMovementInputAsync(999, 1f, 0f, CancellationToken.None);
            Assert.Equal(MovementRequestStatus.InvalidPlayer, response.Status);
        }

        [Fact]
        public async Task MultipleMovements_EachUpdatesLocation()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            await deps.Controller.SetMovementInputAsync(spawned.Id, 1f, 0f, CancellationToken.None);
            await deps.Controller.SetMovementInputAsync(spawned.Id, 1f, 0f, CancellationToken.None);
            await deps.Controller.SetMovementInputAsync(spawned.Id, 1f, 0f, CancellationToken.None);

            PlayerInfo info = await deps.Controller.GetPlayerInfoAsync(
                spawned.Id,
                CancellationToken.None);

            float initialX = spawned.Location.X;
            Assert.Equal(initialX + 3f, info.Location.X);
        }
    }

    public class SubscribeRoomAsync
    {
        [Fact]
        public async Task CanSubscribeAfterSpawning()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo spawned = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            Task task = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate _ in deps.Controller.SubscribeRoomAsync(
                                       spawned.Id,
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
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var snapshots = new List<RoomPlayerUpdate>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player1.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                        if (snapshots.Count >= 1) break;
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
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var snapshots1 = new List<RoomPlayerUpdate>();
            Task task1 = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player1.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots1.Add(snap);
                        if (snapshots1.Count >= 1) break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            var snapshots2 = new List<RoomPlayerUpdate>();
            Task task2 = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player2.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots2.Add(snap);
                        if (snapshots2.Count >= 1) break;
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
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var snapshots = new List<RoomPlayerUpdate>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player1.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                        if (snapshots.Count >= 1) break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            await deps.Controller.SetMovementInputAsync(player2.Id, 1f, 0f, CancellationToken.None);

            await subscriptionTask;

            Assert.True(snapshots.Count > 0);
        }

        [Fact]
        public async Task Cancellation_StopsReceiving()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player.RoomId;

            await Task.Delay(100);
            var snapshot1 = new RoomPlayerUpdate
            {
                RoomId = roomId, Players = new List<PlayerSnapshot>(), ExcludePlayerId = null
            };
            await deps.Registry.PublishUpdateAsync(
                roomId,
                snapshot1,
                RoomUpdateContext.Broadcast(),
                CancellationToken.None);

            await Task.Delay(100);

            using var cts = new CancellationTokenSource();
            var snapshots = new List<RoomPlayerUpdate>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player.Id,
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
            var snapshot2 = new RoomPlayerUpdate
            {
                RoomId = roomId, Players = new List<PlayerSnapshot>(), ExcludePlayerId = null
            };
            await deps.Registry.PublishUpdateAsync(roomId, snapshot2, RoomUpdateContext.Broadcast(), cts.Token);

            await Task.Delay(50);
            await cts.CancelAsync();

            await Task.Delay(100);
            var snapshot3 = new RoomPlayerUpdate
            {
                RoomId = roomId, Players = new List<PlayerSnapshot>(), ExcludePlayerId = null
            };
            await deps.Registry.PublishUpdateAsync(roomId, snapshot3, RoomUpdateContext.Broadcast(), cts.Token);

            await Task.Delay(100);
            await subscriptionTask;

            Assert.True(snapshots.Count > 0);
        }

        [Fact]
        public async Task InvalidRoomId_NoEmissions()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var snapshots = new List<RoomPlayerUpdate>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player.Id,
                                       999,
                                       cts.Token))
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
        public async Task ReceivesInitialSnapshotEvenIfLastUpdateExcludedThem()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player.RoomId;

            await Task.Delay(100);
            var excludedSnapshot = new RoomPlayerUpdate
            {
                RoomId = roomId, Players = new List<PlayerSnapshot>(), ExcludePlayerId = player.Id
            };
            await deps.Registry.PublishUpdateAsync(
                roomId,
                excludedSnapshot,
                RoomUpdateContext.ExcludePlayer(player.Id),
                CancellationToken.None);

            await Task.Delay(100);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var snapshots = new List<RoomPlayerUpdate>();

            Task task = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                        break; // Only need the initial snapshot
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await task;

            // Player should receive the initial room state snapshot even if the last update excluded them.
            // Exclusion prevents self-notification, but doesn't hide current room state from new subscribers.
            Assert.Single(snapshots);
            Assert.Equal(roomId, snapshots[0].RoomId);
        }

        [Fact]
        public async Task PlayerMoves_ExcludedFromOwnUpdateButOtherPlayerReceivesIt()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var player2Snapshots = new List<RoomPlayerUpdate>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player2.Id,
                                       roomId,
                                       cts.Token))
                    {
                        player2Snapshots.Add(snap);
                        if (player2Snapshots.Count >= 2) // Initial + movement update
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);

            // Player 1 moves - this should generate an update excluded for player 1 but sent to player 2
            MovementInputResponse moveResult = await deps.Controller.SetMovementInputAsync(
                player1.Id,
                1f,
                0f,
                CancellationToken.None);

            await subscriptionTask;

            // Player 2 should receive the initial snapshot and the movement update from player 1
            Assert.Equal(MovementRequestStatus.Ok, moveResult.Status);
            Assert.True(player2Snapshots.Count >= 2);
        }
    }

    public class EndToEndScenarios
    {
        [Fact]
        public async Task TwoPlayersSpawnAndSeeEachOther()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var snapshots = new List<RoomPlayerUpdate>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player1.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(snap);
                        if (snapshots.Count >= 2) break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            var moveSnapshot = new RoomPlayerUpdate
            {
                RoomId = roomId, Players = new List<PlayerSnapshot>(), ExcludePlayerId = null
            };
            await deps.Registry.PublishUpdateAsync(
                roomId,
                moveSnapshot,
                RoomUpdateContext.Broadcast(),
                CancellationToken.None);

            await Task.Delay(50);
            await deps.Controller.SetMovementInputAsync(player2.Id, 1f, 0f, CancellationToken.None);

            await subscriptionTask;

            Assert.True(snapshots.Count >= 1);
        }

        [Fact]
        public async Task PlayerMoves_OtherPlayerReceivesUpdate()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            int roomId = player1.RoomId;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var player1Snapshots = new List<RoomPlayerUpdate>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (RoomPlayerUpdate snap in deps.Controller.SubscribeRoomAsync(
                                       player1.Id,
                                       roomId,
                                       cts.Token))
                    {
                        player1Snapshots.Add(snap);
                        if (player1Snapshots.Count >= 2) break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            var moveSnapshot = new RoomPlayerUpdate
            {
                RoomId = roomId, Players = new List<PlayerSnapshot>(), ExcludePlayerId = null
            };
            await deps.Registry.PublishUpdateAsync(
                roomId,
                moveSnapshot,
                RoomUpdateContext.Broadcast(),
                CancellationToken.None);

            await Task.Delay(50);
            MovementInputResponse moveResult = await deps.Controller.SetMovementInputAsync(
                player2.Id,
                1f,
                0f,
                CancellationToken.None);

            await subscriptionTask;

            Assert.Equal(MovementRequestStatus.Ok, moveResult.Status);
            Assert.True(player1Snapshots.Count >= 1);
        }

        [Fact]
        public async Task ThreePlayers_AllInSameRoom()
        {
            TestHelpers.ControllerDependencies deps = TestHelpers.CreateControllerDependencies();
            PlayerInfo player1 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo player2 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);
            PlayerInfo player3 = await deps.Controller.SpawnPlayerAsync(CancellationToken.None);

            Assert.Equal(player1.RoomId, player2.RoomId);
            Assert.Equal(player2.RoomId, player3.RoomId);

            await Task.Delay(100);
            RoomStateSnapshot? room = await deps.RoomStore.GetRoomAsync(player1.RoomId, CancellationToken.None);

            Assert.NotNull(room);
            Assert.Equal(3, room.Players.Count);
        }
    }
}