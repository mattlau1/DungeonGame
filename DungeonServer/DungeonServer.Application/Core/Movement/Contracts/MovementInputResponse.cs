using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Movement.Contracts;

public record struct MovementInputResponse(MovementRequestStatus Status, Location Location, int RoomId);