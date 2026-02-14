using DungeonGame.Core;
using DungeonServer.Application.Core.Movement.Contracts;
using DungeonServer.Application.Core.Movement.Models;
using DungeonServer.Service.Mappings.Shared;

namespace DungeonServer.Service.Mappings.Core;

public static class MovementControllerMappings
{
    public static SetMovementInputResponse ToProto(this MovementInputResponse appResponse)
    {
        return new SetMovementInputResponse
        {
            StatusResponse = appResponse.Status switch
            {
                MovementRequestStatus.Ok => MovementInputStatusResult.Ok,
                MovementRequestStatus.Blocked => MovementInputStatusResult.Blocked,
                MovementRequestStatus.TooFast => MovementInputStatusResult.TooFast,
                MovementRequestStatus.InvalidPlayer => MovementInputStatusResult.InvalidPlayer,
                _ => MovementInputStatusResult.Unspecified
            },
            AuthoritativeLocation = appResponse.Location.ToProto(),
            DebugMessage = appResponse.DebugMsg
        };
    }
}
