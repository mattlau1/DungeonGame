namespace DungeonServer.Application.Core.Shared;

public sealed record Location(float X, float Y)
{
    public static Location Origin => new(0, 0);

    public float UnrootedDistanceTo(Location other)
    {
        // Return squared distance between this location and other (avoid sqrt for comparisons).
        float dx = this.X - other.X;
        float dy = this.Y - other.Y;
        return dx * dx + dy * dy;
    }
}