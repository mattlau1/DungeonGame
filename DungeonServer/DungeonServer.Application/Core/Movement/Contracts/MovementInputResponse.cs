using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Movement.Contracts;

public record MovementInputResponse(MovementRequestStatus status, Location location, string debugMsg = "");