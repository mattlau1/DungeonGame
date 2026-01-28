using DungeonServer.Application.Core.Movement.Contracts;

namespace DungeonServer.Application.Abstractions.Core;

public interface IMovementManager
{
    Task<MovementInputResponse> SetMovementInput(MovementInputRequest moveRequest, CancellationToken ct);
}