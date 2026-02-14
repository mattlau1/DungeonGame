using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Shared;

public static class Helpers
{
    public static bool LocationIsWithinBounds(Location location, float maxX, float maxY)
    {
        return location.X >= 0 && location.Y >= 0 && location.X < maxX && location.Y < maxY;
    }

    public static Direction GetOppositeDirection(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            _ => throw new ArgumentException("Invalid direction", nameof(direction))
        };
    }

    /// <summary>
    /// Calculates the new location for a player transitioning between rooms.
    /// Uses percentage-based scaling to maintain relative position on the shared boundary
    /// and applies a small offset to ensure the player enters clearly inside the new room.
    /// </summary>
    /// <param name="oldLocation">The player's location in the old room (likely out of bounds).</param>
    /// <param name="oldRoom">Snapshot of the room the player is leaving.</param>
    /// <param name="newRoom">Snapshot of the room the player is entering.</param>
    /// <param name="exitDirection">The direction of the exit taken from the old room.</param>
    /// <returns>A new <see cref="Location"/> relative to the new room's coordinate space.</returns>
    public static Location GetTransitionedLocation(
        Location oldLocation,
        RoomStateSnapshot oldRoom,
        RoomStateSnapshot newRoom,
        Direction exitDirection)
    {
        const float offset = 0.5f;
        float newX = oldLocation.X;
        float newY = oldLocation.Y;

        switch (exitDirection)
        {
            case Direction.North:
                newX = (newX / oldRoom.Width) * newRoom.Width;
                newY = offset; // Enter at the bottom
                break;
            case Direction.South:
                newX = (newX / oldRoom.Width) * newRoom.Width;
                newY = newRoom.Height - offset; // Enter at the top
                break;
            case Direction.East:
                newX = offset; // Enter at the left
                newY = (newY / oldRoom.Height) * newRoom.Height;
                break;
            case Direction.West:
                newX = newRoom.Width - offset; // Enter at the right
                newY = (newY / oldRoom.Height) * newRoom.Height;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(exitDirection), exitDirection, null);
        }

        return new Location(newX, newY);
    }
}