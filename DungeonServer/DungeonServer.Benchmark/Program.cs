using DungeonServer.Benchmark;

var config = new BenchmarkConfig
{
    WarmupSeconds = 5, TestDurationSeconds = 30
};

var htmlPath = Path.Combine(AppContext.BaseDirectory, "dashboard.html");
using var dashboardServer = new DashboardServer(config.DashboardPort, config.ServerUrl, htmlPath);
dashboardServer.Start();

Console.WriteLine($"Dashboard running at http://localhost:{config.DashboardPort}");
Console.WriteLine($"Benchmark server: {config.ServerUrl}");
Console.WriteLine("Press Ctrl+C to exit");

await Task.Delay(Timeout.Infinite);