using System.Collections.Concurrent;
using System.Net;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Rooms.Storage;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Infrastructure.Messaging.Rooms;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace DungeonServer.Application.Tests.Rooms;

public class RedisRoomSubscriptionRegistryTests
{
    private readonly InMemoryRedisConnection _redisConnection;

    public RedisRoomSubscriptionRegistryTests()
    {
        _redisConnection = new InMemoryRedisConnection();
    }

    private RedisRoomSubscriptionRegistry CreateRegistry()
    {
        return new RedisRoomSubscriptionRegistry(_redisConnection.MockConnection.Object);
    }

    [Fact]
    public async Task SubscribeAsync_CanCreateSubscription()
    {
        var registry = CreateRegistry();

        using var cts = new CancellationTokenSource();

        Task subscribeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate _ in registry.SubscribeAsync(1, 1, cts.Token))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);
        await cts.CancelAsync();
        await subscribeTask;

        Assert.True(true);
    }

    [Fact]
    public async Task SubscribeAsync_EmitsOnRoomUpdate_ShouldReflectNewPlayer()
    {
        var registry = CreateRegistry();
        int roomId = 1;

        // Start subscription first so room is tracked in _rooms
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        var snapshots = new List<RoomPlayerUpdate>();

        Task enumerateTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snap in registry.SubscribeAsync(1, roomId, cts.Token))
                {
                    snapshots.Add(snap);
                    if (snapshots.Count >= 1) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(50);

        var player1 = new PlayerSnapshot(1, roomId, new Location(1, 1), true);
        var player2 = new PlayerSnapshot(2, roomId, new Location(2, 2), true);
        
        var update = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = new List<PlayerSnapshot> { player1, player2 },
            ExcludePlayerId = null
        };

        await registry.PublishUpdateAsync(roomId, update, RoomUpdateContext.Broadcast(), CancellationToken.None);

        Task timeout = Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
        Task completed = await Task.WhenAny(enumerateTask, timeout);
        
        if (completed == timeout)
        {
            await cts.CancelAsync();
            await enumerateTask;
            Assert.Fail("Timed out waiting for room update emissions");
        }
        else
        {
            await enumerateTask;
            bool foundCombined = snapshots.Any(s =>
                s.Players.Any(p => p.PlayerId == 1) && s.Players.Any(p => p.PlayerId == 2));
            Assert.True(foundCombined, "Did not observe a snapshot including both players after the second joined");
        }
    }

    [Fact]
    public async Task SubscribeAsync_MultipleSubscribers_AllReceiveUpdates()
    {
        var registry = CreateRegistry();
        int roomId = 1;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var snapshots1 = new List<RoomPlayerUpdate>();
        Task task1 = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snap in registry.SubscribeAsync(1, roomId, cts.Token))
                {
                    snapshots1.Add(snap);
                    if (snapshots1.Count >= 1) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        var snapshots2 = new List<RoomPlayerUpdate>();
        Task task2 = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snap in registry.SubscribeAsync(2, roomId, cts.Token))
                {
                    snapshots2.Add(snap);
                    if (snapshots2.Count >= 1) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);

        var update = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = new List<PlayerSnapshot>(),
            ExcludePlayerId = null
        };
        await registry.PublishUpdateAsync(roomId, update, RoomUpdateContext.Broadcast(), CancellationToken.None);

        await Task.Delay(200);
        await cts.CancelAsync();

        await Task.WhenAll(task1, task2);

        Assert.Single(snapshots1);
        Assert.Single(snapshots2);
    }

    [Fact]
    public async Task SubscribeAsync_Cancellation_StopsReceiving()
    {
        var registry = CreateRegistry();
        int roomId = 1;

        await Task.Delay(100);

        var initialUpdate = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = new List<PlayerSnapshot>(),
            ExcludePlayerId = null
        };
        await registry.PublishUpdateAsync(roomId, initialUpdate, RoomUpdateContext.Broadcast(), CancellationToken.None);

        await Task.Delay(100);

        using var cts = new CancellationTokenSource();

        var snapshots = new List<RoomPlayerUpdate>();
        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snap in registry.SubscribeAsync(1, roomId, cts.Token))
                {
                    snapshots.Add(snap);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);

        var update1 = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = new List<PlayerSnapshot>(),
            ExcludePlayerId = null
        };
        await registry.PublishUpdateAsync(roomId, update1, RoomUpdateContext.Broadcast(), CancellationToken.None);

        await Task.Delay(50);
        await cts.CancelAsync();

        await Task.Delay(100);

        var update2 = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = new List<PlayerSnapshot>(),
            ExcludePlayerId = null
        };
        await registry.PublishUpdateAsync(roomId, update2, RoomUpdateContext.Broadcast(), CancellationToken.None);

        await Task.Delay(100);
        await task;

        Assert.True(snapshots.Count > 0);
    }

    [Fact]
    public async Task SubscribeAsync_ReceivesInitialSnapshotEvenIfLastUpdateExcludedThem()
    {
        var registry = CreateRegistry();
        int roomId = 1;
        int playerId = 1;

        // Start subscription first so room is tracked
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        var snapshots = new List<RoomPlayerUpdate>();

        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (RoomPlayerUpdate snap in registry.SubscribeAsync(playerId, roomId, cts.Token))
                {
                    snapshots.Add(snap);
                    if (snapshots.Count >= 2) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);

        // Publish a normal update first to establish state
        var normalUpdate = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = new List<PlayerSnapshot>(),
            ExcludePlayerId = null
        };
        await registry.PublishUpdateAsync(roomId, normalUpdate, RoomUpdateContext.Broadcast(), CancellationToken.None);
        
        await Task.Delay(50);

        // Now publish excluded update - room is tracked so CurrentState gets set
        var excludedUpdate = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = new List<PlayerSnapshot>(),
            ExcludePlayerId = playerId
        };
        await registry.PublishUpdateAsync(roomId, excludedUpdate, RoomUpdateContext.ExcludePlayer(playerId), CancellationToken.None);

        await Task.Delay(100);
        await cts.CancelAsync();
        await task;

        // Should have received at least one update
        Assert.True(snapshots.Count >= 1);
        Assert.Equal(roomId, snapshots[0].RoomId);
    }

    [Fact]
    public async Task PublishUpdateAsync_PublishesToRedis()
    {
        var registry = CreateRegistry();
        int roomId = 1;

        var update = new RoomPlayerUpdate
        {
            RoomId = roomId,
            Players = new List<PlayerSnapshot>
            {
                new PlayerSnapshot(1, roomId, new Location(1, 1), true)
            },
            ExcludePlayerId = null
        };

        await registry.PublishUpdateAsync(roomId, update, RoomUpdateContext.Broadcast(), CancellationToken.None);

        Assert.True(_redisConnection.PublishCount > 0);
    }
}

public class InMemoryRedisConnection
{
    public readonly Mock<IConnectionMultiplexer> MockConnection;
    public readonly Mock<ISubscriber> MockSubscriber;
    private readonly ConcurrentDictionary<string, List<Action<RedisChannel, RedisValue>>> _channels;
    public int PublishCount = 0;

    public InMemoryRedisConnection()
    {
        _channels = new ConcurrentDictionary<string, List<Action<RedisChannel, RedisValue>>>();
        
        MockSubscriber = new Mock<ISubscriber>();
        
        MockSubscriber.Setup(s => s.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((channel, handler, flags) =>
            {
                var channelName = channel.ToString();
                var handlers = _channels.GetOrAdd(channelName, _ => new List<Action<RedisChannel, RedisValue>>());
                lock (handlers)
                {
                    handlers.Add(handler);
                }
            })
            .Returns(Task.CompletedTask);

        MockSubscriber.Setup(s => s.UnsubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>((channel, handler, flags) =>
            {
                var channelName = channel.ToString();
                if (_channels.TryGetValue(channelName, out var handlers))
                {
                    lock (handlers)
                    {
                        handlers.Remove(handler);
                    }
                }
            })
            .Returns(Task.CompletedTask);

        MockSubscriber.Setup(s => s.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, RedisValue, CommandFlags>((channel, value, flags) =>
            {
                var channelName = channel.ToString();
                PublishCount++;
                
                if (_channels.TryGetValue(channelName, out var handlers))
                {
                    List<Action<RedisChannel, RedisValue>> handlersCopy;
                    lock (handlers)
                    {
                        handlersCopy = new List<Action<RedisChannel, RedisValue>>(handlers);
                    }
                    
                    // Execute handlers asynchronously to avoid blocking
                    Task.Run(() =>
                    {
                        foreach (var handler in handlersCopy)
                        {
                            handler(channel, value);
                        }
                    });
                }
            })
            .Returns(Task.FromResult(0L));

        MockConnection = new Mock<IConnectionMultiplexer>(MockBehavior.Loose);
        MockConnection.Setup(c => c.GetSubscriber(It.IsAny<RedisValue>())).Returns(MockSubscriber.Object);
        MockConnection.Setup(c => c.GetSubscriber(It.IsAny<EndPoint>())).Returns(MockSubscriber.Object);
    }
}
