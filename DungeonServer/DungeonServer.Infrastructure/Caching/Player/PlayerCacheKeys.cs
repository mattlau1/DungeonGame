namespace DungeonServer.Infrastructure.Caching.Player;

public static class PlayerCacheKeys
{
    public const string Count = "player:count";
    public const string FirstActive = "player:first-active";

    public static string Player(int id) => $"player:{id}";
}