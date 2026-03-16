using Grpc.Net.Client;
using Grpc.Core;
using DungeonGame.Core;
using System.Diagnostics;

namespace DungeonServer.Benchmark;

public class VirtualPlayer
{
    private int _playerId;
    private readonly string _serverUrl;
    private readonly MetricsCollector _metrics;
    private readonly bool _enableRoomTransitions;
    private readonly int _movementIntervalMs;
    private readonly int? _maxLifetimeMs;
    private readonly int? _spawnDelayMs;
    private readonly GrpcChannel? _sharedChannel;

    private GrpcChannel? _channel;
    private DungeonController.DungeonControllerClient? _client;
    private Task? _subscriptionTask;
    private CancellationTokenSource? _cts;

    private int _roomId;
    private float _roomWidth;
    private float _roomHeight;
    private float _x;
    private float _y;
    private readonly Random _random = new();
    private bool _isRunning;
    private DateTime _createdAt;

    public bool IsConnected { get; private set; }
    public DateTime CreatedAt => _createdAt;
    public int? MaxLifetimeMs => _maxLifetimeMs;

    public VirtualPlayer(
        int playerId,
        string serverUrl,
        MetricsCollector metrics,
        bool enableRoomTransitions,
        int movementHz,
        int? maxLifetimeMs = null,
        int? spawnDelayMs = null)
        : this(playerId, serverUrl, metrics, enableRoomTransitions, movementHz, maxLifetimeMs, spawnDelayMs, null)
    {
    }

    public VirtualPlayer(
        int playerId,
        string serverUrl,
        MetricsCollector metrics,
        bool enableRoomTransitions,
        int movementHz,
        int? maxLifetimeMs,
        int? spawnDelayMs,
        GrpcChannel? sharedChannel)
    {
        _playerId = playerId;
        _serverUrl = serverUrl;
        _metrics = metrics;
        _enableRoomTransitions = enableRoomTransitions;
        _movementIntervalMs = movementHz > 0 ? 1000 / movementHz : 0;
        _maxLifetimeMs = maxLifetimeMs;
        _spawnDelayMs = spawnDelayMs;
        _random = new Random(playerId + Environment.TickCount);
        _sharedChannel = sharedChannel;
    }

    public async Task ConnectAndSpawnAsync()
    {
        if (_spawnDelayMs.HasValue && _spawnDelayMs.Value > 0)
        {
            await Task.Delay(_random.Next(_spawnDelayMs.Value));
        }

        try
        {
            // Use shared channel if provided, otherwise create new one
            _channel = _sharedChannel ?? GrpcChannel.ForAddress(_serverUrl);
            _client = new DungeonController.DungeonControllerClient(_channel);

            var spawnStopwatch = Stopwatch.StartNew();
            var spawnResponse = await _client.SpawnPlayerAsync(new SpawnRequest());
            spawnStopwatch.Stop();

            _metrics.RecordSpawnLatency(spawnStopwatch.Elapsed);

            _playerId = spawnResponse.Id;
            _roomId = spawnResponse.RoomId;
            float x = spawnResponse.Location.X;
            float y = spawnResponse.Location.Y;

            var roomInfo = await _client.GetRoomInfoAsync(new RoomInfoRequest { RoomId = _roomId });
            _roomWidth = roomInfo.Width;
            _roomHeight = roomInfo.Height;
            
            const float margin = 1.0f;
            _x = Math.Clamp(x, margin, _roomWidth - margin);
            _y = Math.Clamp(y, margin, _roomHeight - margin);
            
            Console.WriteLine($"Player {_playerId} spawned in room {_roomId} with size {_roomWidth}x{_roomHeight} at ({_x:F2},{_y:F2})");

            _metrics.RegisterPlayer(_playerId);

            _createdAt = DateTime.UtcNow;

            _cts = new CancellationTokenSource();
            var subscribeStopwatch = Stopwatch.StartNew();
            var roomStream = _client.SubscribeRoom(
                new SubscribeRoomRequest { PlayerId = _playerId, RoomId = _roomId },
                cancellationToken: _cts.Token);

            _subscriptionTask = Task.Run(
                async () =>
                {
                    int updateCount = 0;
                    var latencyStopwatch = new Stopwatch();
                    
                    try
                    {
                        while (await roomStream.ResponseStream.MoveNext(_cts.Token))
                        {
                            updateCount++;
                            var snapshot = roomStream.ResponseStream.Current;
                            
                            // Start timing after first update (which is instant from cache)
                            if (updateCount == 1)
                            {
                                latencyStopwatch.Start();
                            }
                            else
                            {
                                latencyStopwatch.Stop();
                                var elapsed = latencyStopwatch.Elapsed;
                                _metrics.RecordRoomUpdateLatency(elapsed);
                                
                                latencyStopwatch.Restart();
                            }

                            // Update our knowledge of room state
                            foreach (var player in snapshot.Players)
                            {
                                if (player.Id == _playerId)
                                {
                                    _roomId = player.RoomId;
                                    _metrics.RecordPlayerLocation(_playerId, player.Location.X, player.Location.Y, _roomId);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                    {
                        // Expected - client cancelled
                    }
                    catch (IOException)
                    {
                        // Also expected when the underlying transport is reset.
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Player {_playerId} subscription error: {ex.Message}");
                        _metrics.RecordFailure(_playerId, "SubscriptionError", ex.Message, 0, ex.StackTrace);
                    }
                },
                _cts.Token);

            IsConnected = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Player {_playerId} connection failed: {ex.Message}");
            _metrics.RecordFailure(_playerId, "ConnectionFailed", ex.Message, 0, ex.StackTrace);
            IsConnected = false;

            // Cleanup on partial failure
            if (_playerId != 0 && _client != null)
            {
                try
                {
                    await _client.DisconnectPlayerAsync(
                        new DisconnectRequest { PlayerId = _playerId },
                        deadline: DateTime.UtcNow.AddSeconds(1));
                }
                catch
                {
                    /* Ignore cleanup errors */
                }
            }
        }
    }

    public async Task StartMovementLoopAsync()
    {
        if (!IsConnected || _client == null || _movementIntervalMs <= 0) 
            return;

        _isRunning = true;

        float localX = _x;
        float localY = _y;
        uint sequence = 0;

        try
        {
            using var movementStream = _client.SendInputCommand();

            while (_isRunning && !_cts?.IsCancellationRequested == true)
            {
                if (_maxLifetimeMs.HasValue && (DateTime.UtcNow - _createdAt).TotalMilliseconds > _maxLifetimeMs.Value)
                {
                    break;
                }

                var (inputX, inputY) = CalculateMovementInput(localX, localY);

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        _cts?.Token ?? CancellationToken.None,
                        cts.Token);

                    await movementStream.RequestStream.WriteAsync(
                        new InputCommandRequest { PlayerId = _playerId, InputX = inputX, InputY = inputY, Sequence = sequence++ },
                        linkedCts.Token);

                    // Response is now Empty - we get position updates via SubscribeRoom instead
                    // Just record successful send for latency metrics
                    _metrics.RecordMovementLatency(TimeSpan.Zero, true);
                }
                catch (OperationCanceledException)
                {
                    // Expected on timeout or shutdown
                }
                catch (RpcException ex)
                {
                    _metrics.RecordFailure(_playerId, $"RpcException_{ex.StatusCode}", ex.Message, 0, ex.StackTrace);
                }
                catch (Exception ex)
                {
                    _metrics.RecordFailure(_playerId, ex.GetType().Name, ex.Message, 0, ex.StackTrace);
                }

                try
                {
                    await Task.Delay(_movementIntervalMs, _cts?.Token ?? CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Player {_playerId} movement loop error: {ex.Message}");
        }
    }

    private (float x, float y) CalculateMovementInput(float currentX, float currentY)
    {
        var angle = _random.NextDouble() * Math.PI * 2;
        var distance = 2.0 + _random.NextDouble() * 3.0;
    
        float inputX = (float)(Math.Cos(angle) * distance);
        float inputY = (float)(Math.Sin(angle) * distance);

        if (!_enableRoomTransitions)
        {
            const float margin = 1.0f;
        
            float predictedX = currentX + inputX;
            float predictedY = currentY + inputY;

            float clampedX = Math.Clamp(predictedX, margin, _roomWidth - margin);
            float clampedY = Math.Clamp(predictedY, margin, _roomHeight - margin);

            inputX = clampedX - currentX;
            inputY = clampedY - currentY;
        }

        return (inputX, inputY);
    }

    public async Task DisconnectAsync()
    {
        _isRunning = false;
        _cts?.Cancel();

        // Explicitly notify server of disconnection
        if (_client != null && _playerId != 0)
        {
            try
            {
                await _client.DisconnectPlayerAsync(
                    new DisconnectRequest { PlayerId = _playerId },
                    deadline: DateTime.UtcNow.AddSeconds(2));
            }
            catch
            {
                /* Ignore cleanup errors */
            }
        }

        if (_subscriptionTask != null)
        {
            try
            {
                await _subscriptionTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (TimeoutException)
            {
                // Timeout waiting for subscription - ignore
            }
            catch (Exception)
            {
                // Ignore other exceptions during disconnect
            }
        }

        // Only dispose channel if it was created by this player (not shared)
        if (_channel != null && _sharedChannel == null)
        {
            _channel.Dispose();
        }
        IsConnected = false;
    }
}