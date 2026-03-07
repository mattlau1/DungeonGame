using DungeonServer.Application.Core.Player.Models;

namespace DungeonServer.Application.Core.Rooms.Models;

public sealed record RoomPlayerUpdate
{
    public IReadOnlyCollection<PlayerSnapshot> Players { get; init; } = [];
}
