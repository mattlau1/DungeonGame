namespace DungeonServer.Application.Core.Rooms.Contracts;

public sealed record SubscribeRoomRequest(int PlayerId, int RoomId);