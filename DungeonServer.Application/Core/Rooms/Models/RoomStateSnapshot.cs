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

    public RoomStateSnapshot(int roomId, RoomType roomType, int width, int height, HashSet<int> playerIds)
    {
        RoomId = roomId;
        RoomType = roomType;
        Width = width;
        Height = height;
        PlayerIds = playerIds;
    }

    public static RoomStateSnapshot From(RoomState state)
    {
        return new RoomStateSnapshot(state.RoomId, state.RoomType, state.Width, state.Height, state.PlayerIds);
    }
}