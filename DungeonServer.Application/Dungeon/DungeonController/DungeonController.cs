using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Contracts;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Dungeon.DungeonController;

public sealed class DungeonController : IDungeonController
{
    private readonly IDungeonArchitect _dungeonArchitect;
    private readonly IRoomStore _roomStore;

    private readonly IPlayerStore _playerStore;

    public DungeonController(IDungeonArchitect dungeonArchitect, IRoomStore roomStore, IPlayerStore playerStore)
    {
        _roomStore = roomStore;
        _playerStore = playerStore;
        _dungeonArchitect = dungeonArchitect;
    }

    public async Task<PlayerInfoResult> SpawnPlayerAsync(CancellationToken ct)
    {
        var request = new GenerateRoomRequest();
        GenerateRoomResult startingRoom = await _dungeonArchitect.GenerateRoomAsync(request, ct);

        // TODO: Change this to room entry point or choose middle if it is the spawn room
        var spawnLocation = new Location(
            X: startingRoom.RoomStateSnapshot.Width / 2f,
            Y: startingRoom.RoomStateSnapshot.Height / 2f);

        PlayerSnapshot player = await _playerStore.CreatePlayerAsync(spawnLocation, ct);

        await _roomStore.UpdateRoomAsync(
            startingRoom.RoomStateSnapshot.RoomId,
            room => room.PlayerIds.Add(player.PlayerId),
            ct);

        PlayerSnapshot updated = await _playerStore.UpdatePlayerAsync(
            player.PlayerId,
            info =>
            {
                info.RoomId = startingRoom.RoomStateSnapshot.RoomId;
                info.Location = spawnLocation;
            },
            ct);

        var playerInfo = new PlayerInfo
        {
            Id = updated.PlayerId,
            RoomId = updated.RoomId,
            Location = updated.Location
        };

        return new PlayerInfoResult(updated.RoomId, playerInfo);
    }

    public async Task<PlayerInfoResult> GetPlayerInfoAsync(int playerId, CancellationToken ct)
    {
        PlayerSnapshot? player = await _playerStore.GetPlayerAsync(playerId, ct);
        if (player == null)
        {
            throw new KeyNotFoundException($"Player Id {playerId} does not exist.");
        }

        var playerInfo = new PlayerInfo
        {
            Id = player.PlayerId,
            RoomId = player.RoomId,
            Location = player.Location
        };

        return new PlayerInfoResult(player.RoomId, playerInfo);
    }
}