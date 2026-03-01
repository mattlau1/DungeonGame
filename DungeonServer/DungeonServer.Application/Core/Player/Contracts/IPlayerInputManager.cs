using DungeonServer.Application.Core.Player.Models;

namespace DungeonServer.Application.Core.Player.Contracts;

public interface IPlayerInputManager
{
    public void EnqueueCommand(InputCommand command);

    public List<InputCommand> DequeueAllForPlayer(int playerId);

    public void RemovePlayer(int playerId);
}