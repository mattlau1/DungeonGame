using DungeonServer.Application.Core.Player.Models;

namespace DungeonServer.Application.Core.Rooms.Models;

public sealed record RoomPlayerUpdate
{
    public int RoomId { get; init; }

    public IReadOnlyCollection<PlayerSnapshot> Players { get; init; } = [];

    public int? ExcludePlayerId { get; init; }
}
