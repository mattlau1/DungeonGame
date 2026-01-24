namespace DungeonServer.Application.Dungeon.Room;

public class RoomState
{
    public int RoomId { get; set; }
    public RoomType RoomType { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    // TODO: Add Players, Monsters etc.
}