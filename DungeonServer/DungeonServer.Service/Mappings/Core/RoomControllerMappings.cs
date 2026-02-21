using DungeonGame.Core;
using DungeonServer.Application.Core.Rooms.Models;
using RoomType = DungeonServer.Application.Core.Rooms.Models.RoomType;

namespace DungeonServer.Service.Mappings.Core;

public static class RoomControllerMappings
{
    public static RoomInfo ToProto(this RoomStateSnapshot snapshot)
    {
        return new RoomInfo
        {
            RoomId = snapshot.RoomId,
            Width = snapshot.Width,
            Height = snapshot.Height,
            RoomType = snapshot.RoomType switch
            {
                RoomType.Combat => DungeonGame.Core.RoomType.Combat,
                RoomType.Treasure => DungeonGame.Core.RoomType.Treasure,
                RoomType.Boss => DungeonGame.Core.RoomType.Boss,
                _ => DungeonGame.Core.RoomType.Unknown
            }
        };
    }
}