namespace DungeonServer.Application.Core.Movement.Contracts;

public interface IMovementManager
{
    Task<MovementInputResponse> SetMovementInput(int playerId, float inputX, float inputY, CancellationToken ct);
}