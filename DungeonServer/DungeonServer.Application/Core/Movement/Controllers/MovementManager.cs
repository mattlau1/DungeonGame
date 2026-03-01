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
    private readonly IRoomStore _roomStore;

    public MovementManager(IRoomStore roomStore)
    {
        _roomStore = roomStore;
    }

    public async Task SimulatePhysics(
        PlayerState player,
        List<InputCommand> commands,
        RoomStateSnapshot? room,
        CancellationToken ct)
    {
        float totalMoveX = 0;
        float totalMoveY = 0;
        float newViewAngle = player.ViewAngle;

        foreach (InputCommand cmd in commands)
        {
            totalMoveX += cmd.Input.MoveX;
            totalMoveY += cmd.Input.MoveY;
            newViewAngle = cmd.Input.ViewAngle;
        }

        var targetPosition = new Location(player.Position.X + totalMoveX, player.Position.Y + totalMoveY);

        player.Position = targetPosition;
        player.ViewAngle = newViewAngle;

        if (room != null && room.IsOutOfRoomBounds(player.Position))
        {
            Direction direction = room.GetClosestWallDirection(player.Position);

            if (room.Exits.TryGetValue(direction, out int newRoomId))
            {
                RoomStateSnapshot? newRoom = await _roomStore.GetRoomAsync(newRoomId, ct);

                if (newRoom != null)
                {
                    int oldRoomId = player.RoomId;

                    player.Position = Helpers.GetTransitionedLocation(player.Position, room, newRoom, direction);
                    player.RoomId = newRoomId;

                    await _roomStore.SwapRoomsAsync(player.PlayerId, oldRoomId, newRoomId, ct);
                }
            }
        }
    }
}