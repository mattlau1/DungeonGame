using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Rooms.Player.Models;

public record PlayerInfo
{
    public int Id;
    public int RoomId;
    public required Location Location;
    
    // TODO: Add visible stats like HP
}