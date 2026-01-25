namespace DungeonServer.Application.Dungeon.Rooms.Models;

public class RoomState
{
    public int RoomId { get; set; }
    public RoomType RoomType { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    // TODO: Add Players, Monsters etc.

    public RoomState(RoomType roomType, int width, int height)
    {
        RoomId = 0;
        RoomType = roomType;
        Width = width;
        Height = height;
    }
    
}