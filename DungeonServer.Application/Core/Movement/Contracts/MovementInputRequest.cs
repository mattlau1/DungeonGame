using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Movement.Contracts;

public record MovementInputRequest(int PlayerId, Location RequestedLocation);