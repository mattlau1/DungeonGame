namespace DungeonServer.Application.Core.Rooms.Models;

public sealed record RoomUpdateContext
{
    public static RoomUpdateContext Broadcast() => new();

    public static RoomUpdateContext ExcludePlayer(int playerId) => new(playerId);

    private RoomUpdateContext(int? excludePlayerId = null)
    {
        ExcludePlayerId = excludePlayerId;
    }

    public int? ExcludePlayerId { get; }
}