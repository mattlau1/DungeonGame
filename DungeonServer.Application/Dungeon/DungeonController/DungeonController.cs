using DungeonServer.Application.Abstractions.Core;
using DungeonServer.Application.Abstractions.Dungeon;
using DungeonServer.Application.Core.Rooms.Contracts;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Player.Contracts;
using DungeonServer.Application.Core.Rooms.Player.Models;
using DungeonServer.Application.Core.Rooms.Player.Storage;

namespace DungeonServer.Application.Dungeon.DungeonController;

public sealed class DungeonController : IDungeonController
{
    private readonly IDungeonArchitect _dungeonArchitect;
    private readonly IRoomStore _roomStore;

    private readonly IPlayerStore _playerStore;
    private readonly IMovementManager _movementManager;

    public DungeonController(
        IDungeonArchitect dungeonArchitect,
        IRoomStore roomStore,
        IPlayerStore playerStore,
        IMovementManager movementManager)
    {
        _roomStore = roomStore;
        _playerStore = playerStore;
        _dungeonArchitect = dungeonArchitect;
        _movementManager = movementManager;
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
            RoomUpdateContext.Broadcast(),
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

    public async Task<MovementInputResponse> SetMovementInputAsync(
        int playerId,
        float inputX,
        float inputY,
        CancellationToken ct)
    {
        PlayerInfoResult currPlayer = await GetPlayerInfoAsync(playerId, ct);
        Location currLocation = currPlayer.PlayerInfo.Location;

        var destination = new Location(currLocation.X + inputX, currLocation.Y + inputY);

        var appRequest = new MovementInputRequest(playerId, destination);

        MovementInputResponse appResponse =
            await _movementManager.SetMovementInput(appRequest, ct);

        await _roomStore.PublishRoomUpdateAsync(currPlayer.RoomId, RoomUpdateContext.ExcludePlayer(playerId), ct);

        return appResponse;
    }

    public IAsyncEnumerable<RoomStateSnapshot> SubscribeRoomAsync(int playerId, int roomId, CancellationToken ct)
    {
        return _roomStore.SubscribeRoomAsync(roomId, playerId, ct);
    }
}