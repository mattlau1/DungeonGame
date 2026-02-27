using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Player.Models;

/// <summary>
/// Immutable view of a player's current state.
/// </summary>
public sealed record PlayerSnapshot(int PlayerId, int RoomId, Location Location, bool IsOnline)
{
    public static PlayerSnapshot From(PlayerInfo playerInfo)
    {
        return new PlayerSnapshot(playerInfo.Id, playerInfo.RoomId, playerInfo.Location, playerInfo.IsOnline);
    }

    public PlayerInfo ToPlayerInfo()
    {
        return new PlayerInfo { Id = PlayerId, RoomId = RoomId, Location = Location, IsOnline = IsOnline };
    }
}