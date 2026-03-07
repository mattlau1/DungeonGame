using DungeonServer.Application.Core.Shared;

namespace DungeonServer.Application.Core.Movement.Contracts;

public record struct MovementInputRequest(int PlayerId, Location RequestedLocation);