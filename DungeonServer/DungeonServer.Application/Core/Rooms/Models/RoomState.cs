namespace DungeonServer.Application.Core.Rooms.Models;

public class RoomState
{
    public int RoomId { get; set; }

    public RoomType RoomType { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public HashSet<int> PlayerIds { get; set; } = [];

    public Dictionary<Direction, int> Exits { get; set; } = [];
    
    public RoomState(RoomType roomType, int width, int height)
    {
        RoomId = 0;
        RoomType = roomType;
        Width = width;
        Height = height;
    }
}