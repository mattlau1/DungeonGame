using ApplicationLocation = DungeonServer.Application.Core.Shared.Location;
using ProtoLocation = DungeonGame.Shared.Location;

namespace DungeonServer.Service.Mappings.Shared;

public static class SharedMappings
{
    public static ProtoLocation ToProto(this ApplicationLocation location)
    {
        return new ProtoLocation
        {
            X = location.X,
            Y = location.Y
        };
    }
}