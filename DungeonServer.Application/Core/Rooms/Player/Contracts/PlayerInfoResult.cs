using DungeonServer.Application.Core.Rooms.Player.Models;

namespace DungeonServer.Application.Core.Rooms.Player.Contracts;

public record PlayerInfoResult(int RoomId, PlayerInfo PlayerInfo);