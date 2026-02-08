using DungeonServer.Application.Core.Rooms.Models;

namespace DungeonServer.Application.Core.Rooms.Contracts;

public sealed record GenerateRoomResult(RoomStateSnapshot RoomStateSnapshot); // TODO: Add debug info?