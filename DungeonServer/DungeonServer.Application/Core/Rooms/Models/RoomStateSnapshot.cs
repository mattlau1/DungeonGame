using DungeonServer.Application.Core.Player.Models;
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

    public IReadOnlyCollection<PlayerSnapshot> Players { get; init; } = [];

    public IReadOnlyDictionary<Direction, int> Exits { get; init; } = new Dictionary<Direction, int>();

    public RoomStateSnapshot(int roomId, RoomType roomType, int width, int height, IReadOnlyCollection<PlayerSnapshot> players)
    {
        RoomId = roomId;
        RoomType = roomType;
        Width = width;
        Height = height;
        Players = players;
        Exits = new Dictionary<Direction, int>();
    }

    public RoomStateSnapshot(
        int roomId,
        RoomType roomType,
        int width,
        int height,
        IReadOnlyCollection<PlayerSnapshot> players,
        IReadOnlyDictionary<Direction, int> exits)
    {
        RoomId = roomId;
        RoomType = roomType;
        Width = width;
        Height = height;
        Players = players;
        Exits = exits;
    }

    public static RoomStateSnapshot From(RoomState state)
    {
        return new RoomStateSnapshot(
            state.RoomId,
            state.RoomType,
            state.Width,
            state.Height,
            new List<PlayerSnapshot>(state.Players),
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