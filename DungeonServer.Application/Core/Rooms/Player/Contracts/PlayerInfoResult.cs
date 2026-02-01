using DungeonServer.Application.Core.Player.Models;

namespace DungeonServer.Application.Core.Player.Contracts;

public record PlayerInfoResult(int RoomId, PlayerInfo PlayerInfo);