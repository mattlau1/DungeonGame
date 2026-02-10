using System.Collections.ObjectModel;

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
}