using ApplicationLocation = DungeonServer.Application.Core.Shared.Location;
using GrpcLocation = DungeonGame.Shared.Location;

namespace DungeonServer.Service.Mappings.Shared;

public static class SharedMappings
{
    public static GrpcLocation ToGrpcLocation(this ApplicationLocation location)
    {
        return new GrpcLocation
        {
            X = location.X,
            Y = location.Y
        };
    }
}