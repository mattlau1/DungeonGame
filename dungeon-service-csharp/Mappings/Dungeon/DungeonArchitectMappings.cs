using DungeonGame.Application.Dungeon.GenerateRoom;
using DungeonGame.Dungeon;
using ApplicationRoomType = DungeonGame.Application.Dungeon.GenerateRoom.RoomType;
using GrpcRoomType = DungeonGame.Dungeon.RoomType;

namespace DungeonService.Mappings.Dungeon;

public static class DungeonArchitectMappings
{
    public static DungeonRoom ToGrpcDungeonRoom(this GenerateRoomResult result)
    {
        return new DungeonRoom
        {
            RoomId = result.RoomId,
            RoomType = result.RoomType.ToGrpcRoomType(),
            Width = result.Width,
            Height = result.Height
        };
    }

    public static GrpcRoomType ToGrpcRoomType(this ApplicationRoomType roomType)
    {
        return roomType switch
        {
            ApplicationRoomType.Combat => GrpcRoomType.Combat,
            ApplicationRoomType.Treasure => GrpcRoomType.Treasure,
            ApplicationRoomType.Boss => GrpcRoomType.Boss,
            _ => GrpcRoomType.Unknown
        };
    }
}