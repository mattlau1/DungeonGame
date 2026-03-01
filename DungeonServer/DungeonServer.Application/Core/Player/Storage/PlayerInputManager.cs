using System.Collections.Concurrent;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;

namespace DungeonServer.Application.Core.Player.Storage;

public class PlayerInputManager : IPlayerInputManager
{
    private readonly ConcurrentDictionary<int, PlayerCommandQueue> _queues = new();

    public void EnqueueCommand(InputCommand command)
    {
        PlayerCommandQueue queue = _queues.GetOrAdd(command.PlayerId, _ => new PlayerCommandQueue());

        queue.TryEnqueue(command);
    }

    public List<InputCommand> DequeueAllForPlayer(int playerId)
    {
        if (_queues.TryGetValue(playerId, out PlayerCommandQueue? queue))
        {
            return queue.DequeueAll();
        }

        return [];
    }

    public void RemovePlayer(int playerId)
    {
        _queues.TryRemove(playerId, out _);
    }
}