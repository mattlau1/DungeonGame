using DungeonServer.Application.Abstractions.Core;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Movement.Storage;

public class MovementManager : IMovementManager
{
    private const float SpeedLimit = 10.0f;

    private readonly IPlayerStore _playerStore;

    public MovementManager(IPlayerStore playerStore)
    {
        _playerStore = playerStore;
    }

    public async Task<MovementInputResponse> SetMovementInput(MovementInputRequest moveRequest, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var status = MovementRequestStatus.Unspecified;

        PlayerSnapshot postMovePlayerSnapshot = await _playerStore.UpdatePlayerAsync(
            moveRequest.PlayerId,
            player =>
            {
                Location current = player.Location;
                var requested = moveRequest.RequestedLocation;

                status = GetMoveRequestStatus(current, requested);
                if (status == MovementRequestStatus.Ok)
                {
                    player.Location = requested;
                }
            },
            ct);

        return new MovementInputResponse(status, postMovePlayerSnapshot.Location);
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
        {
            return MovementRequestStatus.Blocked;
        }

        return MovementRequestStatus.Ok;
    }
}