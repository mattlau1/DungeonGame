namespace DungeonServer.Application.Dungeon.Room;

/// <summary>
/// Similar to RoomState, but immutable.
/// </summary>
public record RoomStateSnapshot
{
    public int RoomId { get; init; }
    public RoomType RoomType { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}