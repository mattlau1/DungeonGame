using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Service.Mappings.Shared;
using PlayerInfo = DungeonGame.Core.PlayerInfo;

namespace DungeonServer.Service.Mappings.Core;

public static class PlayerControllerMappings
{
    public static PlayerInfo ToProto(this PlayerInfoResult result)
    {
        return new PlayerInfo
        {
            RoomId = result.RoomId,
            Id = result.PlayerInfo.Id,
            Location = result.PlayerInfo.Location.ToProto()
        };
    }

    public static PlayerInfo ToProto(this PlayerSnapshot snapshot)
    {
        return new PlayerInfo
        {
            Id = snapshot.PlayerId,
            RoomId = snapshot.RoomId,
            Location = snapshot.Location.ToProto()
        };
    }
}