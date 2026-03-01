using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Movement.Models;

public record PlayerState
{
    public int PlayerId { get; init; }
    public int RoomId { get; set; }
    public required Location Position { get; set; }
    public float ViewAngle { get; set; }
    public uint LastProcessedSequence { get; set; }
    public bool IsOnline { get; set; }
}