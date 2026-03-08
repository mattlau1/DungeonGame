using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Tests;
using Xunit;
using RoomSnapshot = DungeonGame.Core.RoomSnapshot;

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
            Assert.True(result.Id > 0);
            Assert.True(result.RoomId > 0);
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
                    await foreach (ReadOnlyMemory<byte> _ in deps.Controller.SubscribeRoomAsync(
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
            var snapshots = new List<RoomSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (ReadOnlyMemory<byte> bytes in deps.Controller.SubscribeRoomAsync(
                                       player1.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
                        if (snapshots.Count >= 1) break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            await deps.RoomStore.PublishRoomUpdateAsync(roomId, CancellationToken.None);

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

            var snapshots1 = new List<RoomSnapshot>();
            Task task1 = Task.Run(async () =>
            {
                try
                {
                    await foreach (ReadOnlyMemory<byte> bytes in deps.Controller.SubscribeRoomAsync(
                                       player1.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots1.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
                        if (snapshots1.Count >= 1) break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            var snapshots2 = new List<RoomSnapshot>();
            Task task2 = Task.Run(async () =>
            {
                try
                {
                    await foreach (ReadOnlyMemory<byte> bytes in deps.Controller.SubscribeRoomAsync(
                                       player2.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots2.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
                        if (snapshots2.Count >= 1) break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            await deps.RoomStore.PublishRoomUpdateAsync(roomId, CancellationToken.None);

            await Task.Delay(100);

            Assert.Single(snapshots1);
            Assert.Single(snapshots2);
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
                Players = new List<PlayerSnapshot>()
            };
            await deps.Registry.PublishUpdateAsync(
                roomId,
                snapshot1,
                CancellationToken.None);

            await Task.Delay(100);

            using var cts = new CancellationTokenSource();
            var snapshots = new List<RoomSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (ReadOnlyMemory<byte> bytes in deps.Controller.SubscribeRoomAsync(
                                       player.Id,
                                       roomId,
                                       cts.Token))
                    {
                        snapshots.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await Task.Delay(100);
            var snapshot2 = new RoomPlayerUpdate
            {
                Players = new List<PlayerSnapshot>()
            };
            await deps.Registry.PublishUpdateAsync(roomId, snapshot2, cts.Token);

            await Task.Delay(50);
            await cts.CancelAsync();

            await Task.Delay(100);
            var snapshot3 = new RoomPlayerUpdate
            {
                Players = new List<PlayerSnapshot>()
            };
            await deps.Registry.PublishUpdateAsync(roomId, snapshot3, cts.Token);

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
            var snapshots = new List<RoomSnapshot>();

            Task subscriptionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (ReadOnlyMemory<byte> bytes in deps.Controller.SubscribeRoomAsync(
                                       player.Id,
                                       999,
                                       cts.Token))
                    {
                        snapshots.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            await subscriptionTask;

            Assert.Empty(snapshots);
        }
    }

    public class EndToEndScenarios
    {
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