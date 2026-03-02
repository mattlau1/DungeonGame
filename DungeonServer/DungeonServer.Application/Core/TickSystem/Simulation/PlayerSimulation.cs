using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Controllers;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Player.Storage;
using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.TickSystem.Simulation;

public class PlayerSimulation : ISimulation
{
    private readonly IPlayerInputManager _inputManager;
    private readonly PlayerStateManager _playerStateManager;
    private readonly IMovementManager _movementManager;

    public PlayerSimulation(
        IPlayerInputManager inputManager,
        PlayerStateManager playerStateManager,
        IMovementManager movementManager)
    {
        _inputManager = inputManager;
        _playerStateManager = playerStateManager;
        _movementManager = movementManager;
    }

    public async Task SimulateAsync(RoomStateSnapshot room, CancellationToken ct)
    {
        List<PlayerState> players = _playerStateManager.GetPlayersInRoom(room.RoomId);

        foreach (PlayerState player in players)
        {
            List<InputCommand> cmds = _inputManager.DequeueAllForPlayer(player.PlayerId);
            if (cmds.Count > 0)
            {
                await _movementManager.SimulatePhysics(player, cmds, room, ct);
                player.LastProcessedSequence = cmds[^1].Sequence;
            }
        }
    }
}