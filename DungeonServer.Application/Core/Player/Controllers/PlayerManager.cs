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
        IEnumerable<PlayerSnapshot> allPlayers = await _playerStore.GetAllPlayersAsync(ct);
        PlayerSnapshot[] occupants = allPlayers.Where(p => p.RoomId != RoomConstants.InvalidRoomId)
            .OrderBy(p => p.PlayerId)
            .ToArray();

        if (occupants.Length == 0)
        {
            return null;
        }

        (_, int targetRoomId, Location targetLocation) = occupants.First();

        return await SpawnPlayerAtRoom(targetRoomId, targetLocation, ct);
    }

    private async Task<PlayerInfoResult> SpawnPlayerAtRoom(int roomId, Location location, CancellationToken ct)
    {
        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(location, ct);

        await _roomStore.UpdateRoomAsync(
            roomId,
            r => r.PlayerIds.Add(player.PlayerId),
            RoomUpdateContext.Broadcast(),
            ct);

        PlayerSnapshot updated = await _playerStore.UpdatePlayerAsync(
            player.PlayerId,
            info =>
            {
                info.RoomId = roomId;
                info.Location = location;
            },
            ct);

        var playerInfo = new PlayerInfo
        {
            Id = updated.PlayerId,
            RoomId = updated.RoomId,
            Location = updated.Location
        };

        return new PlayerInfoResult(roomId, playerInfo);
    }
}