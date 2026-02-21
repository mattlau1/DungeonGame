using Grpc.Net.Client;
using System.Diagnostics;
using System.Text.Json;
using DungeonGame.Core;

namespace DungeonServer.Benchmark;

public class BenchmarkRunner
{
    private readonly BenchmarkConfig _config;
    private readonly DashboardServer _dashboard;
    private readonly MetricsCollector _metrics;
    private readonly List<VirtualPlayer> _players = new();
    private readonly List<BenchmarkResult> _results = new();
    private readonly CancellationToken _cancellationToken;
    private DungeonController.DungeonControllerClient? _client;

    public BenchmarkRunner(
        BenchmarkConfig config,
        DashboardServer dashboard,
        CancellationToken cancellationToken = default)
    {
        _config = config;
        _dashboard = dashboard;
        _cancellationToken = cancellationToken;
        _metrics = new MetricsCollector();
        _metrics.SetLogFilePath("benchmark_failures.json");
        _metrics.OnMetricsUpdated += m => _dashboard.UpdateMetrics(m);
    }

    public async Task RunAllScenariosAsync()
    {
        var channel = GrpcChannel.ForAddress(_config.ServerUrl);
        _client = new DungeonController.DungeonControllerClient(channel);

        try
        {
            var spawnResponse = await _client.SpawnPlayerAsync(
                new SpawnRequest(),
                deadline: DateTime.UtcNow.AddSeconds(5));
            await _client.DisconnectPlayerAsync(
                new DisconnectRequest { PlayerId = spawnResponse.Id },
                deadline: DateTime.UtcNow.AddSeconds(5));
        }
        catch
        {
            throw new Exception("Could not connect to server");
        }

        foreach (var scenario in _config.Scenarios)
        {
            if (_cancellationToken.IsCancellationRequested) break;
            await RunScenarioAsync(scenario);
        }

        _dashboard.UpdateCurrentTest("All Tests Complete", _results.Sum(r => r.PlayerCount), "Complete");

        SaveResults();
    }

    private async Task RunScenarioAsync(TestScenario scenario)
    {
        if (scenario.PlayerCounts != null)
        {
            foreach (var playerCount in scenario.PlayerCounts)
            {
                if (_cancellationToken.IsCancellationRequested) break;
                await RunPlayerCountTestAsync(scenario, playerCount);
            }
        }
        else if (scenario.MovementRates != null && scenario.PlayerCount.HasValue)
        {
            foreach (var rate in scenario.MovementRates)
            {
                if (_cancellationToken.IsCancellationRequested) break;
                await RunFrequencyTestAsync(scenario, scenario.PlayerCount.Value, rate);
            }
        }
    }

    private async Task WaitForServerDrainAsync(DungeonController.DungeonControllerClient client)
    {
        _dashboard.UpdateCurrentTest("Waiting for server drain...", 0, "Cleanup");

        int count = -1;
        int retryCount = 0;
        while (retryCount < 30 && !_cancellationToken.IsCancellationRequested)
        {
            try
            {
                var status = await client.GetServerStatusAsync(
                    new Google.Protobuf.WellKnownTypes.Empty(),
                    deadline: DateTime.UtcNow.AddSeconds(2));
                count = status.ActivePlayerCount;
                Console.WriteLine("Server active players: " + count);
                if (count == 0)
                {
                    return; // Server is clean
                }
            }
            catch
            {
                // Ignore transient errors during cleanup
            }

            await Task.Delay(1000, _cancellationToken); // Wait 1 second before polling again
            retryCount++;
        }

        if (count > 0)
        {
            throw new Exception(
                $"Server did not drain properly after {retryCount} retries. Remaining players: {count}");
        }
    }

    private async Task RunPlayerCountTestAsync(TestScenario scenario, int playerCount)
    {
        _dashboard.UpdateCurrentTest(scenario.Name, playerCount, "Spawning");

        _metrics.Reset();
        _metrics.SetCurrentScenario(scenario.Name);
        _metrics.StartTest(_config.TestDurationSeconds);

        _players.Clear();

        for (int i = 0; i < playerCount; i++)
        {
            var player = new VirtualPlayer(
                i,
                _config.ServerUrl,
                _metrics,
                scenario.EnableRoomTransitions,
                scenario.MovementHz);
            _players.Add(player);
            await player.ConnectAndSpawnAsync();
            await Task.Delay(20, _cancellationToken);
        }

        foreach (var player in _players)
        {
            _ = player.StartMovementLoopAsync();
        }

        _dashboard.UpdateCurrentTest(scenario.Name, playerCount, "Running");

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed.TotalSeconds < _config.TestDurationSeconds)
        {
            await Task.Delay(1000, _cancellationToken);

            var snapshot = _metrics.GetSnapshot();
            _dashboard.UpdateMetrics(snapshot);
        }

        foreach (var player in _players)
        {
            await player.DisconnectAsync();
        }

        _players.Clear();

        var channel = GrpcChannel.ForAddress(_config.ServerUrl);
        var client = new DungeonController.DungeonControllerClient(channel);
        await WaitForServerDrainAsync(client);

        var finalMetrics = _metrics.GetSnapshot();
        var result = new BenchmarkResult
        {
            ScenarioName = scenario.Name,
            PlayerCount = playerCount,
            MovementHz = scenario.MovementHz,
            Metrics = finalMetrics
        };
        _results.Add(result);
        _dashboard.AddResult(result);
        _dashboard.UpdateCurrentTest(scenario.Name, playerCount, "Complete");
        
        _metrics.SaveFailureLog();
    }

    private async Task RunFrequencyTestAsync(TestScenario scenario, int playerCount, int movementHz)
    {
        var originalHz = scenario.MovementHz;
        scenario.MovementHz = movementHz;

        await RunPlayerCountTestAsync(scenario, playerCount);

        scenario.MovementHz = originalHz;
    }

    private void SaveResults()
    {
        _metrics.SaveFailureLog();

        var results = new { Timestamp = DateTime.UtcNow, Results = _results };

        var json = JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        File.WriteAllText("benchmark_results.json", json);
    }
}

public class BenchmarkResult
{
    public string ScenarioName { get; set; } = "";
    public int PlayerCount { get; set; }
    public int MovementHz { get; set; }
    public MetricsSnapshot Metrics { get; set; } = new();
}