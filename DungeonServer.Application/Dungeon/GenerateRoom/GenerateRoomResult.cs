namespace DungeonGame.Application.Dungeon.GenerateRoom;

public sealed record GenerateRoomResult(int RoomId, RoomType RoomType, int Width, int Height);