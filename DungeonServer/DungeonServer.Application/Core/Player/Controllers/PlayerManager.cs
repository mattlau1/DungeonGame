using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Contracts;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Application.Core.TickSystem.Simulation;

namespace DungeonServer.Application.Core.Player.Controllers;

public class PlayerManager : IPlayerManager
{
    private readonly IDungeonArchitect _dungeonArchitect;   
    private readonly IPlayerStore _playerStore;
    private readonly IRoomStore _roomStore;
    private readonly PlayerStateManager _playerStateManager;
    private readonly ISimulationQueue _simulationQueue;

    public PlayerManager(
        IDungeonArchitect dungeonArchitect,
        IPlayerStore playerStore,
        IRoomStore roomStore,
        PlayerStateManager playerStateManager,
        ISimulationQueue simulationQueue)
    {
        _dungeonArchitect = dungeonArchitect;
        _playerStore = playerStore;
        _roomStore = roomStore;
        _playerStateManager = playerStateManager;
        _simulationQueue = simulationQueue;
    }

    public async Task<PlayerInfo> SpawnPlayerAsync(CancellationToken ct)
    {
        PlayerInfo? existingJoin = await TryJoinExistingRoomAsync(ct);
        if (existingJoin.HasValue)
        {
            return existingJoin.Value;
        }

        return await SpawnPlayerInNewRoom(ct);
    }

    private async Task<PlayerInfo> SpawnPlayerInNewRoom(CancellationToken ct)
    {
        var request = new GenerateRoomRequest();
        GenerateRoomResult startingRoom = await _dungeonArchitect.GenerateRoomAsync(request, ct);

        var spawnLocation = new Location(
            X: startingRoom.RoomStateSnapshot.Width / 2f,
            Y: startingRoom.RoomStateSnapshot.Height / 2f);

        return await SpawnPlayerAtRoom(startingRoom.RoomStateSnapshot.RoomId, spawnLocation, ct);
    }

    private async Task<PlayerInfo?> TryJoinExistingRoomAsync(CancellationToken ct)
    {
        PlayerSnapshot? occupant = await _playerStore.GetFirstActivePlayerAsync(ct);

        if (occupant == null || occupant.RoomId == RoomConstants.InvalidRoomId)
        {
            return null;
        }

        return await SpawnPlayerAtRoom(occupant.RoomId, occupant.Location, ct);
    }

    private async Task<PlayerInfo> SpawnPlayerAtRoom(int roomId, Location location, CancellationToken ct)
    {
        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(location, ct);

        await _roomStore.AddPlayerToRoomAsync(roomId, player.PlayerId, ct);

        PlayerSnapshot updated = await _playerStore.UpdateLocationAsync(
            player.PlayerId,
            location,
            roomId,
            ct);

        _playerStateManager.AddPlayerToRoom(updated.PlayerId, updated.RoomId, updated.Location);

        // Register player input simulation if this is the first player in the room
        if (_playerStateManager.GetPlayersInRoom(roomId).Count == 1)
        {
            _simulationQueue.Register(roomId, typeof(PlayerSimulation));
        }

        return new PlayerInfo
        {
            Id = updated.PlayerId,
            RoomId = updated.RoomId,
            Location = updated.Location
        };
    }

    public async Task DisconnectPlayerAsync(int playerId, CancellationToken ct)
    {
        PlayerSnapshot? player = await _playerStore.GetPlayerAsync(playerId, ct);
        if (player == null)
        {
            return;
        }

        if (player.IsOnline)
        {
            await _roomStore.RemovePlayerFromRoomAsync(player.RoomId, playerId, ct);
        }

        _playerStateManager.RemovePlayerFromRoom(playerId);

        // Unregister player input simulation if no players remain in the room
        if (_playerStateManager.GetPlayersInRoom(player.RoomId).Count == 0)
        {
            _simulationQueue.Unregister(player.RoomId, typeof(PlayerSimulation));
        }

        await _playerStore.DisconnectPlayerAsync(playerId, ct);
    }
}
