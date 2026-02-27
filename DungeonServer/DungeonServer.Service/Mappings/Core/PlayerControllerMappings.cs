using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Service.Mappings.Shared;
using PlayerInfo = DungeonGame.Core.PlayerInfo;
using AppPlayerInfo = DungeonServer.Application.Core.Player.Models.PlayerInfo;

namespace DungeonServer.Service.Mappings.Core;

public static class PlayerControllerMappings
{
    public static PlayerInfo ToProto(this AppPlayerInfo info)
    {
        return new PlayerInfo { RoomId = info.RoomId, Id = info.Id, Location = info.Location.ToProto() };
    }

    public static PlayerInfo ToProto(this PlayerSnapshot snapshot)
    {
        return new PlayerInfo
        {
            Id = snapshot.PlayerId, RoomId = snapshot.RoomId, Location = snapshot.Location.ToProto()
        };
    }
}