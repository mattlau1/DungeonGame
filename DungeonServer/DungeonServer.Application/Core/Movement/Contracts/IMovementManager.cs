using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Movement.Contracts;

public interface IMovementManager
{
    Task SimulatePhysics(PlayerState player, List<InputCommand> commands, RoomStateSnapshot? room, CancellationToken ct);
}