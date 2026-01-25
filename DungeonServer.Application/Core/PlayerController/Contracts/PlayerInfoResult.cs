using DungeonServer.Application.Core.PlayerController.Models;

namespace DungeonServer.Application.Core.PlayerController.Contracts;

public record PlayerInfoResult(int RoomId, PlayerInfo PlayerInfo);