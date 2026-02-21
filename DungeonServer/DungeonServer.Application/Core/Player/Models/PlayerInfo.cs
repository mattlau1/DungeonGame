using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Player.Models;

public record PlayerInfo
{
    public int Id;
    public int RoomId;
    public required Location Location;
    public bool IsOnline;
    
    // TODO: Add visible stats like HP
}