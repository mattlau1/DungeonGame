using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DungeonServer.Application.Core.Movement.Storage;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Dungeon.DungeonArchitect;
using DungeonServer.Application.Dungeon.DungeonController;
using DungeonServer.Application.Core.Shared;
 using DungeonServer.Service.Services.Core;
 using DungeonServer.Application.Abstractions.Dungeon;
using Xunit;

namespace DungeonServer.Service.Tests.Integration
{
    // Client-like integration: exercise the service edge using the IDungeonController interface.
    public class ClientPathIntegrationTests
    {
        [Fact]
        public async Task ThreePlusPlayers_InSameRoom_SubscriptionUpdatesViaInterface()
        {
            // Setup in-memory stores and service
            var registry = new DungeonServer.Application.Core.Rooms.Storage.RoomSubscriptionRegistry();
            var roomStore = new DungeonServer.Application.Core.Rooms.Storage.InMemoryRoomStore(registry);
            var playerStore = new DungeonServer.Application.Core.Player.Storage.InMemoryPlayerStore();
            var movementManager = new DungeonServer.Application.Core.Movement.Storage.MovementManager(playerStore);
            var architect = new DungeonServer.Application.Dungeon.DungeonArchitect.DungeonArchitect(roomStore);
            var controller = new DungeonServer.Application.Dungeon.DungeonController.DungeonController(architect, roomStore, playerStore, movementManager);

            // Choose a shared room by creating one and then adding players into it
            var starter = await controller.SpawnPlayerAsync(CancellationToken.None);
            int sharedRoomId = starter.PlayerInfo.RoomId;

            // Spawn two additional players and move them into the same room
            var p2 = await controller.SpawnPlayerAsync(CancellationToken.None);
            await roomStore.UpdateRoomAsync(sharedRoomId,
                r => r.PlayerIds.Add(p2.PlayerInfo.Id),
                DungeonServer.Application.Core.Rooms.Models.RoomUpdateContext.Broadcast(),
                CancellationToken.None);
            await playerStore.UpdatePlayerAsync(p2.PlayerInfo.Id, info => info.RoomId = sharedRoomId, CancellationToken.None);

            var p3 = await controller.SpawnPlayerAsync(CancellationToken.None);
            await roomStore.UpdateRoomAsync(sharedRoomId,
                r => r.PlayerIds.Add(p3.PlayerInfo.Id),
                DungeonServer.Application.Core.Rooms.Models.RoomUpdateContext.Broadcast(),
                CancellationToken.None);
            await playerStore.UpdatePlayerAsync(p3.PlayerInfo.Id, info => info.RoomId = sharedRoomId, CancellationToken.None);

            // Client-like subscription via interface
            IDungeonController clientInterface = controller;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var emissions = new List<RoomStateSnapshot>();

            var subTask = Task.Run(async () =>
            {
                await foreach (var snap in clientInterface.SubscribeRoomAsync(starter.PlayerInfo.Id, sharedRoomId, cts.Token))
                {
                    emissions.Add(snap);
                    if (emissions.Count >= 2) break; // observe at least two updates
                }
            });

            // Trigger another update by moving p2 within the same room (no new room creation to keep it in the same room)
            var move = await controller.SetMovementInputAsync(p2.PlayerInfo.Id, 1f, 0f, CancellationToken.None);
            // Wait for emissions to propagate
            var timeout = Task.Delay(TimeSpan.FromSeconds(5));
            var done = await Task.WhenAny(subTask, timeout);
            if (done == timeout)
            {
                cts.Cancel();
                await subTask;
                Assert.True(false, "Did not observe room subscription updates in time");
            }
            else
            {
                await subTask;
                // Verify that the initial emission included all 3 players and the second emission still shows all 3 (or at least the rooms reflect joined players)
                bool allPresent = emissions.Any(s => s.PlayerIds.Contains(starter.PlayerInfo.Id))
                                  && emissions.Any(s => s.PlayerIds.Contains(p2.PlayerInfo.Id))
                                  && emissions.Any(s => s.PlayerIds.Contains(p3.PlayerInfo.Id));
                Assert.True(allPresent, "Subscription did not reflect all players in the room");
            }
        }
    }
}
