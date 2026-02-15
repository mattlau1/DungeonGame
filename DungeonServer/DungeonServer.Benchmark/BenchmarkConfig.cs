namespace DungeonServer.Benchmark;

public class BenchmarkConfig
{
    public string ServerUrl { get; set; } = "http://localhost:5142";
    public int WarmupSeconds { get; set; } = 10;
    public int TestDurationSeconds { get; set; } = 60;
    public int DashboardPort { get; set; } = 8080;
    public List<TestScenario> Scenarios { get; set; } = new();
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
}