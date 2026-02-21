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

    private GrpcChannel? _channel;
    private DungeonController.DungeonControllerClient? _client;
    private Task? _subscriptionTask;
    private CancellationTokenSource? _cts;

    private float _x;
    private float _y;
    private int _roomId;
    private const float RoomWidth = 32;
    private const float RoomHeight = 32;
    private readonly Random _random = new();
    private bool _isRunning;

    public int PlayerId => _playerId;
    public int RoomId => _roomId;
    public float X => _x;
    public float Y => _y;
    public bool IsConnected { get; private set; }

    public VirtualPlayer(
        int playerId,
        string serverUrl,
        MetricsCollector metrics,
        bool enableRoomTransitions,
        int movementHz)
    {
        _playerId = playerId;
        _serverUrl = serverUrl;
        _metrics = metrics;
        _enableRoomTransitions = enableRoomTransitions;
        _movementIntervalMs = 1000 / movementHz;
    }

    public async Task ConnectAndSpawnAsync()
    {
        try
        {
            _channel = GrpcChannel.ForAddress(_serverUrl);
            _client = new DungeonController.DungeonControllerClient(_channel);

            // Spawn player
            var spawnStopwatch = Stopwatch.StartNew();
            var spawnResponse = await _client.SpawnPlayerAsync(new SpawnRequest());
            spawnStopwatch.Stop();

            _metrics.RecordSpawnLatency(spawnStopwatch.Elapsed);

            _playerId = spawnResponse.Id;
            _roomId = spawnResponse.RoomId;
            _x = spawnResponse.Location.X;
            _y = spawnResponse.Location.Y;

            _metrics.RegisterPlayer(_playerId);

            // Subscribe to room updates
            _cts = new CancellationTokenSource();
            var subscribeStopwatch = Stopwatch.StartNew();
            var roomStream = _client.SubscribeRoom(
                new SubscribeRoomRequest { PlayerId = _playerId, RoomId = _roomId },
                cancellationToken: _cts.Token);

            _subscriptionTask = Task.Run(
                async () =>
                {
                    try
                    {
                        while (await roomStream.ResponseStream.MoveNext(_cts.Token))
                        {
                            subscribeStopwatch.Stop();
                            var snapshot = roomStream.ResponseStream.Current;
                            _metrics.RecordRoomUpdateLatency(subscribeStopwatch.Elapsed);
                            subscribeStopwatch.Restart();

                            // Update our knowledge of room state
                            foreach (var player in snapshot.Players)
                            {
                                if (player.Id == _playerId)
                                {
                                    _roomId = player.RoomId;
                                    _x = player.Location.X;
                                    _y = player.Location.Y;
                                    _metrics.RecordPlayerLocation(_playerId, _x, _y, _roomId);
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
        if (!IsConnected || _client == null) return;

        _isRunning = true;

        try
        {
            using var movementStream = _client.SetMovementInput();

            while (_isRunning && !_cts?.IsCancellationRequested == true)
            {
                var stopwatch = Stopwatch.StartNew();

                // Random walk with boundary checking
                var (inputX, inputY) = CalculateMovementInput();

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        _cts?.Token ?? CancellationToken.None,
                        cts.Token);

                    await movementStream.RequestStream.WriteAsync(
                        new SetMovementInputRequest { PlayerId = _playerId, InputX = inputX, InputY = inputY },
                        linkedCts.Token);

                    // Read response
                    var moveNextTask = movementStream.ResponseStream.MoveNext(linkedCts.Token);
                    if (await Task.WhenAny(moveNextTask, Task.Delay(5000, linkedCts.Token)) == moveNextTask &&
                        moveNextTask.Result)
                    {
                        stopwatch.Stop();
                        var response = movementStream.ResponseStream.Current;

                        bool success = response.StatusResponse == MovementInputStatusResult.Ok;
                        _metrics.RecordMovementLatency(stopwatch.Elapsed, success);

                        _x = response.AuthoritativeLocation.X;
                        _y = response.AuthoritativeLocation.Y;

                        if (success)
                        {
                            if (response.RoomId != _roomId)
                            {
                                _metrics.RecordRoomTransition();
                                _roomId = response.RoomId;
                            }

                            _metrics.RecordPlayerLocation(_playerId, _x, _y, _roomId);
                        }
                        else
                        {
                            _metrics.RecordFailure(_playerId, "MovementRejected", $"Server rejected movement: {response.StatusResponse}. Current pos: ({_x:F2}, {_y:F2}), RoomId: {_roomId}, Target: ({inputX:F2}, {inputY:F2})", stopwatch.Elapsed.TotalMilliseconds);
                        }
                    }
                    else
                    {
                        stopwatch.Stop();
                        _metrics.RecordMovementLatency(stopwatch.Elapsed, false);
                        _metrics.RecordFailure(_playerId, "MovementTimeout", "Movement request timed out after 5 seconds - server not responding", stopwatch.Elapsed.TotalMilliseconds);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected on timeout or shutdown
                }
                catch (RpcException ex)
                {
                    _metrics.RecordMovementLatency(stopwatch.Elapsed, false);
                    _metrics.RecordFailure(_playerId, $"RpcException_{ex.StatusCode}", ex.Message, stopwatch.Elapsed.TotalMilliseconds, ex.StackTrace);
                }
                catch (Exception ex)
                {
                    _metrics.RecordMovementLatency(stopwatch.Elapsed, false);
                    _metrics.RecordFailure(_playerId, ex.GetType().Name, ex.Message, stopwatch.Elapsed.TotalMilliseconds, ex.StackTrace);
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

    private (float x, float y) CalculateMovementInput()
    {
        // Random direction
        var angle = _random.NextDouble() * Math.PI * 2;
        var distance = 2.0 + _random.NextDouble() * 3.0; // 2-5 units per tick

        float inputX = (float)(Math.Cos(angle) * distance);
        float inputY = (float)(Math.Sin(angle) * distance);

        // If room transitions disabled, bounce off walls
        if (!_enableRoomTransitions)
        {
            if (_x + inputX < 1 || _x + inputX >= RoomWidth - 1) inputX = -inputX;
            if (_y + inputY < 1 || _y + inputY >= RoomHeight - 1) inputY = -inputY;
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

        _channel?.Dispose();
        IsConnected = false;
    }
}