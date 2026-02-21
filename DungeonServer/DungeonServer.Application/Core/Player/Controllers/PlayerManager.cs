using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Contracts;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Player.Controllers;

public class PlayerManager : IPlayerManager
{
    private readonly IDungeonArchitect _dungeonArchitect;   
    private readonly IPlayerStore _playerStore;
    private readonly IRoomStore _roomStore;

    public PlayerManager(IDungeonArchitect dungeonArchitect, IPlayerStore playerStore, IRoomStore roomStore)
    {
        _dungeonArchitect = dungeonArchitect;
        _playerStore = playerStore;
        _roomStore = roomStore;
    }

    public async Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct)
    {
        PlayerInfoResult? existingJoin = await TryJoinExistingRoomAsync(ct);
        if (existingJoin is not null)
        {
            return existingJoin;
        }

        return await SpawnPlayerInNewRoom(ct);
    }

    private async Task<PlayerInfoResult> SpawnPlayerInNewRoom(CancellationToken ct)
    {
        var request = new GenerateRoomRequest();
        GenerateRoomResult startingRoom = await _dungeonArchitect.GenerateRoomAsync(request, ct);

        var spawnLocation = new Location(
            X: startingRoom.RoomStateSnapshot.Width / 2f,
            Y: startingRoom.RoomStateSnapshot.Height / 2f);

        return await SpawnPlayerAtRoom(startingRoom.RoomStateSnapshot.RoomId, spawnLocation, ct);
    }

    private async Task<PlayerInfoResult?> TryJoinExistingRoomAsync(CancellationToken ct)
    {
        PlayerSnapshot? occupant = await _playerStore.GetFirstActivePlayerAsync(ct);

        if (occupant == null)
        {
            return null;
        }

        return await SpawnPlayerAtRoom(occupant.RoomId, occupant.Location, ct);
    }

    private async Task<PlayerInfoResult> SpawnPlayerAtRoom(int roomId, Location location, CancellationToken ct)
    {
        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(location, ct);

        await _roomStore.AddPlayerToRoomAsync(roomId, player.PlayerId, ct);

        PlayerSnapshot updated = await _playerStore.UpdateLocationAsync(
            player.PlayerId,
            location,
            roomId,
            ct);

        var playerInfo = new PlayerInfo
        {
            Id = updated.PlayerId,
            RoomId = updated.RoomId,
            Location = updated.Location
        };

        return new PlayerInfoResult(roomId, playerInfo);
    }

    public async Task DisconnectPlayerAsync(int playerId, CancellationToken ct)
    {
        PlayerSnapshot? player = await _playerStore.GetPlayerAsync(playerId, ct);
        if (player == null)
        {
            return;
        }

        if (player.RoomId != RoomConstants.InvalidRoomId)
        {
            await _roomStore.RemovePlayerFromRoomAsync(player.RoomId, playerId, ct);
        }

        await _playerStore.DeletePlayerAsync(playerId, ct);
    }
}
