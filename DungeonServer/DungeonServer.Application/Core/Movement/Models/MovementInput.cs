namespace DungeonServer.Application.Core.Movement.Models;

public record struct MovementInput
{
    public float MoveX { get; init; } // -1 to +1 (left/right)                      
    public float MoveY { get; init; } // -1 to +1 (forward/backward)                
    public float ViewAngle { get; init; } // 0 to 2π radians (facing direction)         
}