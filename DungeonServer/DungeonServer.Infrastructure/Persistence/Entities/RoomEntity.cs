using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Infrastructure.Persistence.Entities;

public class RoomEntity
{
    public int Id { get; set; }

    public RoomType Type { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public ICollection<PlayerEntity> Occupants { get; set; } = new HashSet<PlayerEntity>();

    public ICollection<RoomExitEntity> Exits { get; set; } = new HashSet<RoomExitEntity>();
}