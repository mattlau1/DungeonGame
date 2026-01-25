using DungeonGame.Core;
using DungeonServer.Application.Core.PlayerController.Contracts;
using DungeonServer.Service.Mappings.Shared;

namespace DungeonServer.Service.Mappings.Core;

public static class PlayerControllerMappings
{
    public static PlayerInfo ToGrpcPlayerInfo(this PlayerInfoResult result)
    {
        return new PlayerInfo
        {
            Id = result.Id,
            RoomId = result.RoomId,
            Location = result.Location.ToGrpcLocation()
        };
    }
}