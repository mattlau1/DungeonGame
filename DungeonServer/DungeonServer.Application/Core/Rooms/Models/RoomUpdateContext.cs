namespace DungeonServer.Application.Core.Rooms.Models;

public sealed record RoomUpdateContext
{
    /// <summary>
    /// Creates a context that broadcasts the update to all subscribers.
    /// All players will receive this update as a notification.
    /// </summary>
    public static RoomUpdateContext Broadcast() => new();

    /// <summary>
    /// Creates a context that excludes a specific player from receivi  ng this update as a notification.
    /// This is typically used when a player triggers their own action (like movement) to prevent
    /// redundant self-notification. The excluded player will still receive the initial room state
    /// snapshot when subscribing, just not subsequent updates they trigger themselves.
    /// </summary>
    /// <param name="playerId">The player ID to exclude from receiving this update notification</param>
    public static RoomUpdateContext ExcludePlayer(int playerId) => new(playerId);

    private RoomUpdateContext(int? excludePlayerId = null)
    {
        ExcludePlayerId = excludePlayerId;
    }

    /// <summary>
    /// The player ID that should be excluded from receiving this update notification.
    /// If null, the update is broadcast to all subscribers.
    /// </summary>
    public int? ExcludePlayerId { get; }
}