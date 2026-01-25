using DungeonGame.Core;
using DungeonServer.Application.Core.Player.Contracts;
using DungeonServer.Service.Mappings.Shared;

namespace DungeonServer.Service.Mappings.Core;

public static class PlayerControllerMappings
{
    public static PlayerInfo ToGrpcPlayerInfo(this PlayerInfoResult result)
    {
        return new PlayerInfo
        {
            RoomId = result.RoomId,
            Id = result.PlayerInfo.Id,
            Location = result.PlayerInfo.Location.ToGrpcLocation()
        };
    }
}