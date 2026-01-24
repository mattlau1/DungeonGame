using DungeonServer.Application.Dungeon.Rooms.Models;

namespace DungeonServer.Application.Dungeon.Rooms.Contracts;

public sealed record GenerateRoomResult(RoomStateSnapshot RoomStateSnapshot); // TODO: Add debug info?