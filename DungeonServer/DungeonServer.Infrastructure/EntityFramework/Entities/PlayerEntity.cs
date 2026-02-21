namespace DungeonServer.Infrastructure.EntityFramework.Entities;

public class PlayerEntity
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsOnline { get; set; }
}