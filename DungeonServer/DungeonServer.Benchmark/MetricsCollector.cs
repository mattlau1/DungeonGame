using System.Collections.Concurrent;

namespace DungeonServer.Benchmark;

public class TimeBoundedLatencyTracker
{
    private readonly ConcurrentQueue<(double Latency, DateTime Timestamp)> _samples = new();
    private readonly TimeSpan _window;

    public TimeBoundedLatencyTracker(TimeSpan window)
    {
        _window = window;
    }

    public void Record(double latencyMs)
    {
        _samples.Enqueue((latencyMs, DateTime.UtcNow));
        Trim();
    }

    public double[] GetLatencies()
    {
        Trim();
        return _samples.Select(s => s.Latency).ToArray();
    }

    public void Clear()
    {
        while (_samples.TryDequeue(out _))
        {
        }
    }

    private void Trim()
    {
        var cutoff = DateTime.UtcNow - _window;
        while (_samples.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _samples.TryDequeue(out _);
        }
    }
}

public class MetricsCollector
{
    private TimeBoundedLatencyTracker _spawnLatencies = new(TimeSpan.Zero);
    private TimeBoundedLatencyTracker _movementLatencies = new(TimeSpan.Zero);
    private TimeBoundedLatencyTracker _roomUpdateLatencies = new(TimeSpan.Zero);
    private readonly ConcurrentDictionary<int, PlayerMetrics> _playerMetrics = new();

    private long _totalMovementRequests;
    private long _successfulMovements;
    private long _failedMovements;
    private long _roomTransitions;
    private DateTime _testStartTime;

    public event Action<MetricsSnapshot>? OnMetricsUpdated;

    public void StartTest(int testDurationSeconds)
    {
        var window = TimeSpan.FromSeconds(testDurationSeconds);
        _spawnLatencies = new TimeBoundedLatencyTracker(window);
        _movementLatencies = new TimeBoundedLatencyTracker(window);
        _roomUpdateLatencies = new TimeBoundedLatencyTracker(window);
        _testStartTime = DateTime.UtcNow;
    }

    public void RecordSpawnLatency(TimeSpan latency)
    {
        _spawnLatencies.Record(latency.TotalMilliseconds);
    }

    public void RecordMovementLatency(TimeSpan latency, bool success)
    {
        _movementLatencies.Record(latency.TotalMilliseconds);
        Interlocked.Increment(ref _totalMovementRequests);

        if (success)
            Interlocked.Increment(ref _successfulMovements);
        else
            Interlocked.Increment(ref _failedMovements);
    }

    public void RecordRoomUpdateLatency(TimeSpan latency)
    {
        _roomUpdateLatencies.Record(latency.TotalMilliseconds);
    }

    public void RecordRoomTransition()
    {
        Interlocked.Increment(ref _roomTransitions);
    }

    public void RegisterPlayer(int playerId)
    {
        _playerMetrics[playerId] = new PlayerMetrics { PlayerId = playerId };
    }

    public void RecordPlayerLocation(int playerId, float x, float y, int roomId)
    {
        if (_playerMetrics.TryGetValue(playerId, out var metrics))
        {
            metrics.CurrentX = x;
            metrics.CurrentY = y;
            metrics.CurrentRoomId = roomId;
            metrics.LastUpdate = DateTime.UtcNow;
        }
    }

    public MetricsSnapshot GetSnapshot()
    {
        var snapshot = new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            ElapsedSeconds = (DateTime.UtcNow - _testStartTime).TotalSeconds,
            ActivePlayers = _playerMetrics.Count,
            TotalMovementRequests = Interlocked.Read(ref _totalMovementRequests),
            SuccessfulMovements = Interlocked.Read(ref _successfulMovements),
            FailedMovements = Interlocked.Read(ref _failedMovements),
            RoomTransitions = Interlocked.Read(ref _roomTransitions),

            // Latency in milliseconds
            SpawnLatency = GetLatencyStats(_spawnLatencies),
            MovementLatency = GetLatencyStats(_movementLatencies),
            RoomUpdateLatency = GetLatencyStats(_roomUpdateLatencies),

            // Requests per second
            RequestsPerSecond = CalculateRps()
        };

        OnMetricsUpdated?.Invoke(snapshot);
        return snapshot;
    }

    private LatencyStats GetLatencyStats(TimeBoundedLatencyTracker tracker)
    {
        var values = tracker.GetLatencies();
        if (values.Length == 0) return new LatencyStats();

        Array.Sort(values);

        return new LatencyStats
        {
            Count = values.Length,
            Min = values[0],
            Max = values[^1],
            Mean = values.Average(),
            P50 = GetPercentile(values, 50),
            P95 = GetPercentile(values, 95),
            P99 = GetPercentile(values, 99),
            P999 = GetPercentile(values, 99.9)
        };
    }

    private double GetPercentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        var index = (int)Math.Ceiling(sortedValues.Length * percentile / 100.0) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Length - 1))];
    }

    private double CalculateRps()
    {
        var elapsed = (DateTime.UtcNow - _testStartTime).TotalSeconds;
        return elapsed > 0 ? Interlocked.Read(ref _totalMovementRequests) / elapsed : 0;
    }

    public void Reset()
    {
        _spawnLatencies.Clear();
        _movementLatencies.Clear();
        _roomUpdateLatencies.Clear();
        _playerMetrics.Clear();
        _totalMovementRequests = 0;
        _successfulMovements = 0;
        _failedMovements = 0;
        _roomTransitions = 0;
    }
}

public class PlayerMetrics
{
    public int PlayerId { get; set; }
    public float CurrentX { get; set; }
    public float CurrentY { get; set; }
    public int CurrentRoomId { get; set; }
    public DateTime LastUpdate { get; set; }
}

public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public double ElapsedSeconds { get; set; }
    public int ActivePlayers { get; set; }
    public long TotalMovementRequests { get; set; }
    public long SuccessfulMovements { get; set; }
    public long FailedMovements { get; set; }
    public long RoomTransitions { get; set; }
    public double RequestsPerSecond { get; set; }
    public LatencyStats SpawnLatency { get; set; } = new();
    public LatencyStats MovementLatency { get; set; } = new();
    public LatencyStats RoomUpdateLatency { get; set; } = new();
}

public class LatencyStats
{
    public long Count { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Mean { get; set; }
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public double P999 { get; set; }
}