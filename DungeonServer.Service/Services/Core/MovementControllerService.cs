using DungeonGame.Core;
using Grpc.Core;

namespace DungeonServer.Service.Services.Core;

public class MovementControllerService : MovementController.MovementControllerBase
{
    public override Task<SetMovementInputResponse> SetMovementInput(SetMovementInputRequest request, ServerCallContext context)
    {
        return base.SetMovementInput(request, context);
    }
}