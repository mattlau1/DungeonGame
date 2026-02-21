using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Infrastructure.EntityFramework.Entities;

public class RoomExitEntity
{
    public int FromRoomId { get; set; }

    public Direction ExitDirection { get; set; }

    public int ToRoomId { get; set; }
}