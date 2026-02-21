using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Rooms.Models;

/// <summary>
/// Similar to RoomState but as an immutable record.
/// </summary>
public sealed record RoomStateSnapshot
{
    public int RoomId { get; init; }

    public RoomType RoomType { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public HashSet<int> PlayerIds { get; set; } = [];

    public IReadOnlyDictionary<Direction, int> Exits { get; init; } = new Dictionary<Direction, int>();

    public RoomStateSnapshot(int roomId, RoomType roomType, int width, int height, HashSet<int> playerIds)
    {
        RoomId = roomId;
        RoomType = roomType;
        Width = width;
        Height = height;
        PlayerIds = playerIds;
        Exits = new Dictionary<Direction, int>();
    }

    public RoomStateSnapshot(
        int roomId,
        RoomType roomType,
        int width,
        int height,
        HashSet<int> playerIds,
        IReadOnlyDictionary<Direction, int> exits)
    {
        RoomId = roomId;
        RoomType = roomType;
        Width = width;
        Height = height;
        PlayerIds = playerIds;
        Exits = exits;
    }

    public static RoomStateSnapshot From(RoomState state)
    {
        return new RoomStateSnapshot(
            state.RoomId,
            state.RoomType,
            state.Width,
            state.Height,
            new HashSet<int>(state.PlayerIds),
            new Dictionary<Direction, int>(state.Exits));
    }

    public Direction GetClosestWallDirection(Location location)
    {
        var distances = new[]
        {
            (Dir: Direction.North, Dist: Math.Abs(Height - location.Y)),
            (Dir: Direction.South, Dist: Math.Abs(location.Y)),
            (Dir: Direction.East, Dist: Math.Abs(Width - location.X)),
            (Dir: Direction.West, Dist: Math.Abs(location.X))
        };

        return distances.MinBy(x => x.Dist).Dir;
    }

    public bool IsOutOfRoomBounds(Location location)
    {
        if (location.X < 0)
        {
            return true;
        }

        if (location.X >= Width)
        {
            return true;
        }

        if (location.Y < 0)
        {
            return true;
        }

        if (location.Y >= Height)
        {
            return true;
        }

        return false;
    }
}