namespace DungeonServer.Application.Core.Movement.Contracts;

public interface IMovementManager
{
    Task<MovementInputResponse> SetMovementInput(MovementInputRequest moveRequest, CancellationToken ct);
}