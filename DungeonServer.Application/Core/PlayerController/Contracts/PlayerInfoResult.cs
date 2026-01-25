using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.PlayerController.Contracts;

public record PlayerInfoResult(int Id, int RoomId, Location Location);