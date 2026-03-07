using DungeonServer.Application.Core.Movement.Models;

namespace DungeonServer.Application.Core.Player.Models;

public record struct InputCommand
{
    public required int PlayerId { get; init; }
    public required uint Sequence { get; init; }
    public required long ClientTimestamp { get; init; } // For RTT calculation         
    public required MovementInput Input { get; init; }
}