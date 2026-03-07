using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Player.Models;

public record struct PlayerUpdate(int PlayerId, Location Location, int RoomId);