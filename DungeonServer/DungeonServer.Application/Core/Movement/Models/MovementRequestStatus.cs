namespace DungeonServer.Application.Core.Movement.Models;

public enum MovementRequestStatus
{
    Unspecified = 0,
    Ok = 1,
    Blocked = 2,
    InvalidPlayer = 3,
    TooFast = 4
}