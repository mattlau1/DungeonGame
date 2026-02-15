using DungeonServer.Benchmark;

var config = new BenchmarkConfig
{
    ServerUrl = "http://localhost:5142", WarmupSeconds = 5, TestDurationSeconds = 30, DashboardPort = 8080
};

var htmlPath = Path.Combine(AppContext.BaseDirectory, "dashboard.html");
using var dashboardServer = new DashboardServer(config.DashboardPort, htmlPath);
dashboardServer.Start();

Console.WriteLine($"Dashboard running at http://localhost:{config.DashboardPort}");
Console.WriteLine("Press Ctrl+C to exit");

await Task.Delay(Timeout.Infinite);