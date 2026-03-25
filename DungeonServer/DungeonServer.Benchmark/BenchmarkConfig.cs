namespace DungeonServer.Benchmark;

public class BenchmarkConfig
{
    public string ServerUrl { get; set; } = GetEnvOrDefault("BENCHMARK_SERVER_URL", "http://localhost:5142");
    public int WarmupSeconds { get; set; } = 10;
    public int TestDurationSeconds { get; set; } = 60;
    public int DashboardPort { get; set; } = GetEnvOrDefaultInt("BENCHMARK_DASHBOARD_PORT", 9092);
    public List<TestScenario> Scenarios { get; set; } = new();

    private static string GetEnvOrDefault(string name, string defaultValue)
    {
        return Environment.GetEnvironmentVariable(name) ?? defaultValue;
    }

    private static int GetEnvOrDefaultInt(string name, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}

public class TestScenario
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string TestMode { get; set; } = ""; // "capacity" for auto-finding max players

    // For capacity tests
    public int[]? PlayerCounts { get; set; }

    // For multi-room tests
    public int? RoomCount { get; set; }
    public int? PlayersPerRoom { get; set; }

    // For single-count tests
    public int? PlayerCount { get; set; }

    // For frequency tests
    public int[]? MovementRates { get; set; }

    // Common settings
    public int MovementHz { get; set; } = 60;
    public bool EnableRoomTransitions { get; set; } = false;

    // For churn tests
    public bool EnableChurn { get; set; } = false;
    public int MinLifetimeMs { get; set; } = 1000;
    public int MaxLifetimeMs { get; set; } = 5000;
    public int SpawnDelaySpreadMs { get; set; } = 2000;
}