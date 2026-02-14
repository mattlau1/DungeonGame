using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Movement.Controllers;

public class MovementManager : IMovementManager
{
    private const float SpeedLimit = 10.0f;

    private readonly IPlayerStore _playerStore;
    private readonly IRoomStore _roomStore;

    public MovementManager(IPlayerStore playerStore, IRoomStore roomStore)
    {
        _playerStore = playerStore;
        _roomStore = roomStore;
    }

    public async Task<MovementInputResponse> SetMovementInput(MovementInputRequest moveRequest, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        PlayerSnapshot? player = await _playerStore.GetPlayerAsync(moveRequest.PlayerId, ct);
        if (player == null)
        {
            return new MovementInputResponse(MovementRequestStatus.InvalidPlayer, Location.Origin, RoomConstants.InvalidRoomId);
        }

        RoomStateSnapshot? room = await _roomStore.GetRoomAsync(player.RoomId, ct);
        if (room == null)
        {
            return new MovementInputResponse(MovementRequestStatus.Blocked, player.Location, player.RoomId);
        }

        MovementRequestStatus status = GetMoveRequestStatus(player.Location, moveRequest.RequestedLocation);
        if (status != MovementRequestStatus.Ok)
        {
            return new MovementInputResponse(status, player.Location, player.RoomId);
        }

        (Location destinationLocation, int destinationRoomId) =
            await ResolveDestination(room, moveRequest.RequestedLocation, ct);

        if (destinationRoomId == RoomConstants.InvalidRoomId)
        {
            return new MovementInputResponse(MovementRequestStatus.Blocked, player.Location, player.RoomId);
        }

        if (destinationRoomId != player.RoomId)
        {
            await _roomStore.SwapRoomsAsync(moveRequest.PlayerId, player.RoomId, destinationRoomId, ct);
        }

        await _playerStore.UpdatePlayerAsync(
            moveRequest.PlayerId,
            p =>
            {
                p.Location = destinationLocation;
                p.RoomId = destinationRoomId;
            },
            ct);

        if (destinationRoomId == player.RoomId)
        {
            await _roomStore.PublishRoomUpdateAsync(
                destinationRoomId,
                RoomUpdateContext.ExcludePlayer(moveRequest.PlayerId),
                ct);
        }

        return new MovementInputResponse(MovementRequestStatus.Ok, destinationLocation, destinationRoomId);
    }

    private async Task<(Location Loc, int RoomId)> ResolveDestination(
        RoomStateSnapshot currentRoom,
        Location requestedLocation,
        CancellationToken ct)
    {
        if (!currentRoom.IsOutOfRoomBounds(requestedLocation))
        {
            return (requestedLocation, currentRoom.RoomId);
        }

        Direction exitDirection = currentRoom.GetClosestWallDirection(requestedLocation);

        if (!currentRoom.Exits.TryGetValue(exitDirection, out int roomIdToEnter))
        {
            return (requestedLocation, RoomConstants.InvalidRoomId);
        }

        RoomStateSnapshot? nextRoom = await _roomStore.GetRoomAsync(roomIdToEnter, ct);
        if (nextRoom == null)
        {
            return (requestedLocation, RoomConstants.InvalidRoomId);
        }

        Location transitionedLocation = Helpers.GetTransitionedLocation(
            requestedLocation,
            currentRoom,
            nextRoom,
            exitDirection);

        return (transitionedLocation, roomIdToEnter);
    }

    private static MovementRequestStatus GetMoveRequestStatus(Location currLocation, Location targetLocation)
    {
        // Check squared distance vs squared speed limit to avoid a sqrt
        float sqDist = currLocation.UnrootedDistanceTo(targetLocation);
        if (sqDist > SpeedLimit * SpeedLimit)
        {
            return MovementRequestStatus.TooFast;
        }

        // TODO: Check collisions here
        if (false)
#pragma warning disable CS0162 // Unreachable code detected
        {
            return MovementRequestStatus.Blocked;
        }
#pragma warning restore CS0162 // Unreachable code detected

        return MovementRequestStatus.Ok;
    }
}