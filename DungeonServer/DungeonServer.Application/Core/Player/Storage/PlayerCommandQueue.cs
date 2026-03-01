using System.Threading.Channels;
using DungeonServer.Application.Core.Player.Models;

namespace DungeonServer.Application.Core.Player.Storage;

public class PlayerCommandQueue
{
    private readonly Channel<InputCommand> _commands = Channel.CreateBounded<InputCommand>(
        new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });

    public bool TryEnqueue(InputCommand cmd) => _commands.Writer.TryWrite(cmd);

    public List<InputCommand> DequeueAll()
    {
        var results = new List<InputCommand>();
        while (_commands.Reader.TryRead(out InputCommand? cmd))
        {
            results.Add(cmd);
        }
        
        return results;
    }
}