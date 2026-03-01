using EmbedIO;
using EmbedIO.Actions;
using Swan.Logging;
using System.Text.Json;

namespace DungeonServer.Benchmark;

public class DashboardServer : IDisposable
{
    private readonly int _port;
    private WebServer? _server;
    private readonly List<MetricsSnapshot> _history = [];
    private readonly object _historyLock = new();
    private MetricsSnapshot? _currentMetrics;
    private readonly string _htmlPath;

    private BenchmarkRunner? _runner;
    private CancellationTokenSource? _benchmarkCts;
    private bool _benchmarkComplete;

    public DashboardServer(int port, string htmlPath)
    {
        _port = port;
        _htmlPath = htmlPath;
        Logger.NoLogging();
    }

    public void Start()
    {
        _server = new WebServer(o => o.WithUrlPrefix($"http://localhost:{_port}").WithMode(HttpListenerMode.EmbedIO));

        _server.WithModule(
            new ActionModule(
                "/api/metrics",
                HttpVerbs.Get,
                async ctx =>
                {
                    ctx.Response.ContentType = "application/json";
                    var snapshot = _currentMetrics ?? new MetricsSnapshot();
                    var response = new
                    {
                        snapshot,
                        CurrentTest =
                            new
                            {
                                Name = _currentTestName,
                                PlayerCount = _currentPlayerCount,
                                Status = _currentTestStatus
                            },
                        IsRunning = _runner != null && !(_benchmarkCts?.IsCancellationRequested ?? true) &&
                                    !_benchmarkComplete
                    };
                    var json = JsonSerializer.Serialize(
                        response,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    await ctx.SendStringAsync(json, "application/json", System.Text.Encoding.UTF8);
                }));

        _server.WithModule(
            new ActionModule(
                "/api/history",
                HttpVerbs.Get,
                async ctx =>
                {
                    ctx.Response.ContentType = "application/json";
                    List<MetricsSnapshot> history;
                    lock (_historyLock)
                    {
                        history = new List<MetricsSnapshot>(_history);
                    }

                    var json = JsonSerializer.Serialize(
                        history,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    await ctx.SendStringAsync(json, "application/json", System.Text.Encoding.UTF8);
                }));

        _server.WithModule(
            new ActionModule(
                "/api/results",
                HttpVerbs.Get,
                async ctx =>
                {
                    ctx.Response.ContentType = "application/json";
                    List<BenchmarkResult> results;
                    lock (_resultsLock)
                    {
                        results = new List<BenchmarkResult>(_results);
                    }

                    var json = JsonSerializer.Serialize(
                        results,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    await ctx.SendStringAsync(json, "application/json", System.Text.Encoding.UTF8);
                }));

        _server.WithModule(
            new ActionModule(
                "/api/start",
                HttpVerbs.Post,
                async ctx =>
                {
                    ctx.Response.ContentType = "application/json";

                    if (_runner != null && !(_benchmarkCts?.IsCancellationRequested ?? true))
                    {
                        await ctx.SendStringAsync(
                            "{\"error\":\"Benchmark already running\"}",
                            "application/json",
                            System.Text.Encoding.UTF8);
                        return;
                    }

                    using var reader = new StreamReader(ctx.Request.InputStream);
                    var body = await reader.ReadToEndAsync();
                    var request = JsonSerializer.Deserialize<StartBenchmarkRequest>(
                                      body,
                                      new JsonSerializerOptions
                                          {
                                              PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                          }) ??
                                  new StartBenchmarkRequest();

                    _benchmarkCts = new CancellationTokenSource();
                    lock (_resultsLock)
                    {
                        _results.Clear();
                    }

                    lock (_historyLock)
                    {
                        _history.Clear();
                    }

                    _benchmarkComplete = false;

                    var benchmarkConfig = CreateBenchmarkConfig(request.Type);

                    _runner = new BenchmarkRunner(benchmarkConfig, this, _benchmarkCts!.Token);
                    var runner = _runner;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await runner.RunAllScenariosAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Benchmark error: {ex.Message}");
                        }
                        finally
                        {
                            _runner = null;
                            _benchmarkComplete = true;
                            _currentTestStatus = "Complete";
                        }
                    });

                    await ctx.SendStringAsync(
                        "{\"status\":\"started\"}",
                        "application/json",
                        System.Text.Encoding.UTF8);
                }));

        _server.WithModule(
            new ActionModule(
                "/api/stop",
                HttpVerbs.Post,
                async ctx =>
                {
                    ctx.Response.ContentType = "application/json";

                    _benchmarkCts?.Cancel();
                    _runner = null;
                    _benchmarkComplete = false;
                    _currentTestStatus = "Idle";

                    await ctx.SendStringAsync(
                        "{\"status\":\"stopped\"}",
                        "application/json",
                        System.Text.Encoding.UTF8);
                }));

        _server.WithModule(
            new ActionModule(
                "/",
                HttpVerbs.Get,
                async ctx =>
                {
                    ctx.Response.ContentType = "text/html";
                    if (File.Exists(_htmlPath))
                    {
                        var html = await File.ReadAllTextAsync(_htmlPath);
                        await ctx.SendStringAsync(html, "text/html", System.Text.Encoding.UTF8);
                    }
                    else
                    {
                        await ctx.SendStringAsync(
                            "<html><body><h1>Dashboard not found</h1></body></html>",
                            "text/html",
                            System.Text.Encoding.UTF8);
                    }
                }));

        _server.RunAsync();
    }

    private BenchmarkConfig CreateBenchmarkConfig(string type)
    {
        if (type == "stress")
        {
            return new BenchmarkConfig
            {
                ServerUrl = "http://localhost:5142",
                WarmupSeconds = 0,
                TestDurationSeconds = 60,
                DashboardPort = _port,
                Scenarios = BenchmarkScenarios.GetStressScenarios()
            };
        }

        if (type == "quick")
        {
            return new BenchmarkConfig
            {
                ServerUrl = "http://localhost:5142",
                WarmupSeconds = 1,
                TestDurationSeconds = 10,
                DashboardPort = _port,
                Scenarios = BenchmarkScenarios.GetQuickScenarios()
            };
        }

        return new BenchmarkConfig
        {
            ServerUrl = "http://localhost:5142",
            WarmupSeconds = 3,
            TestDurationSeconds = 30,
            DashboardPort = _port,
            Scenarios = BenchmarkScenarios.GetDefaultScenarios()
        };
    }

    private readonly List<BenchmarkResult> _results = new();
    private readonly object _resultsLock = new();

    public void UpdateMetrics(MetricsSnapshot metrics)
    {
        try
        {
            _currentMetrics = metrics;

            lock (_historyLock)
            {
                _history.Add(metrics);
                if (_history.Count > 300)
                {
                    _history.RemoveAt(0);
                }
            }
        }
        catch
        {
            // ignored
        }
    }

    public void UpdateCurrentTest(string testName, int playerCount, string status)
    {
        _currentTestName = testName;
        _currentPlayerCount = playerCount;
        _currentTestStatus = status;
    }

    public void AddResult(BenchmarkResult result)
    {
        lock (_resultsLock)
        {
            _results.Add(result);
        }
    }

    private string _currentTestName = "";
    private int _currentPlayerCount;
    private string _currentTestStatus = "Idle";

    public void Dispose()
    {
        _benchmarkCts?.Cancel();
        _server?.Dispose();
    }
}

public class StartBenchmarkRequest
{
    public string Type { get; init; } = "benchmark";
}