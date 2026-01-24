using DungeonGame.Dungeon;
using DungeonServer.Application.Dungeon.Rooms.Contracts;
using ApplicationRoomType = DungeonServer.Application.Dungeon.Rooms.Models.RoomType;
using GrpcRoomType = DungeonGame.Dungeon.RoomType;

namespace DungeonServer.Service.Mappings.Dungeon;

public static class DungeonArchitectMappings
{
    public static DungeonRoom ToGrpcDungeonRoom(this GenerateRoomResult result)
    {
        return new DungeonRoom
        {
            RoomId = result.RoomStateSnapshot.RoomId,
            RoomType = result.RoomStateSnapshot.RoomType.ToGrpcRoomType(),
            Width = result.RoomStateSnapshot.Width,
            Height = result.RoomStateSnapshot.Height
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