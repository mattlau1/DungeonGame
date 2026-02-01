using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Player.Models;

/// <summary>
/// Immutable view of a player's current state.
/// </summary>
public sealed record PlayerSnapshot(int PlayerId, int RoomId, Location Location)
{
    public static PlayerSnapshot From(PlayerInfo playerInfo)
    {
        return new PlayerSnapshot(playerInfo.Id, playerInfo.RoomId, playerInfo.Location);
    }
}