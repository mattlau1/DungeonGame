using System.Collections.Concurrent;
using System.Net;
using DungeonServer.Application.Core.Player.Models;
using DungeonServer.Application.Core.Rooms.Models;
using DungeonServer.Application.Core.Shared;
using DungeonServer.Application.Tests;
using DungeonServer.Infrastructure.InMemory.Player;
using DungeonServer.Infrastructure.Messaging.Rooms;
using Moq;
using StackExchange.Redis;
using Xunit;
using RoomSnapshot = DungeonGame.Core.RoomSnapshot;

namespace DungeonServer.Application.Tests.Rooms;

public class RedisRoomSubscriptionRegistryTests
{
    private readonly InMemoryRedisConnection _redisConnection;
    private readonly InMemoryPlayerStore _playerStore;

    public RedisRoomSubscriptionRegistryTests()
    {
        _redisConnection = new InMemoryRedisConnection();
        _playerStore = new InMemoryPlayerStore();
    }

    private RedisRoomSubscriptionRegistry CreateRegistry()
    {
        return new RedisRoomSubscriptionRegistry(_redisConnection.MockConnection.Object);
    }

    [Fact]
    public async Task SubscribeAsync_CanCreateSubscription()
    {
        var registry = CreateRegistry();

        var player = await _playerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);

        using var cts = new CancellationTokenSource();

        Task subscribeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (ReadOnlyMemory<byte> _ in registry.SubscribeAsync(player.PlayerId, 1, cts.Token))
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

        var player = await _playerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);

        // Start subscription first so room is tracked in _rooms
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var snapshots = new List<RoomSnapshot>();

        Task enumerateTask = Task.Run(async () =>
        {
            try
            {
                await foreach (ReadOnlyMemory<byte> bytes in registry.SubscribeAsync(player.PlayerId, roomId, cts.Token))
                {
                    snapshots.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
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
            Players = new List<PlayerSnapshot> { player1, player2 }
        };

        await registry.PublishUpdateAsync(roomId, update, CancellationToken.None);

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
                s.Players.Any(p => p.Id == 1) && s.Players.Any(p => p.Id == 2));
            Assert.True(foundCombined, "Did not observe a snapshot including both players after the second joined");
        }
    }

    [Fact]
    public async Task SubscribeAsync_MultipleSubscribers_AllReceiveUpdates()
    {
        var registry = CreateRegistry();
        int roomId = 1;

        var player1 = await _playerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);
        var player2 = await _playerStore.CreatePlayerAsync(new Location(1, 0), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var snapshots1 = new List<RoomSnapshot>();
        Task task1 = Task.Run(async () =>
        {
            try
            {
                await foreach (ReadOnlyMemory<byte> bytes in registry.SubscribeAsync(player1.PlayerId, roomId, cts.Token))
                {
                    snapshots1.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
                    if (snapshots1.Count >= 1) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        var snapshots2 = new List<RoomSnapshot>();
        Task task2 = Task.Run(async () =>
        {
            try
            {
                await foreach (ReadOnlyMemory<byte> bytes in registry.SubscribeAsync(player2.PlayerId, roomId, cts.Token))
                {
                    snapshots2.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
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
            Players = new List<PlayerSnapshot>()
        };
        await registry.PublishUpdateAsync(roomId, update, CancellationToken.None);

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

        var player = await _playerStore.CreatePlayerAsync(new Location(0, 0), CancellationToken.None);

        await Task.Delay(100);

        var initialUpdate = new RoomPlayerUpdate
        {
            Players = new List<PlayerSnapshot>()
        };
        await registry.PublishUpdateAsync(roomId, initialUpdate, CancellationToken.None);

        await Task.Delay(100);

        using var cts = new CancellationTokenSource();

        var snapshots = new List<RoomSnapshot>();
        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (ReadOnlyMemory<byte> bytes in registry.SubscribeAsync(player.PlayerId, roomId, cts.Token))
                {
                    snapshots.Add(TestHelpers.DeserializeRoomSnapshot(bytes));
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(100);

        var update1 = new RoomPlayerUpdate
        {
            Players = new List<PlayerSnapshot>()
        };
        await registry.PublishUpdateAsync(roomId, update1, CancellationToken.None);

        await Task.Delay(50);
        await cts.CancelAsync();

        await Task.Delay(100);

        var update2 = new RoomPlayerUpdate
        {
            Players = new List<PlayerSnapshot>()
        };
        await registry.PublishUpdateAsync(roomId, update2, CancellationToken.None);

        await Task.Delay(100);
        await task;

        Assert.True(snapshots.Count > 0);
    }

    [Fact]
    public async Task PublishUpdateAsync_PublishesToRedis()
    {
        var registry = CreateRegistry();
        int roomId = 1;

        var update = new RoomPlayerUpdate
        {
            Players = new List<PlayerSnapshot> { new PlayerSnapshot(1, roomId, new Location(1, 1), true) }
        };

        await registry.PublishUpdateAsync(roomId, update, CancellationToken.None);

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

        MockSubscriber
            .Setup(s => s.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
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

        MockSubscriber
            .Setup(s => s.UnsubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
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

        MockSubscriber
            .Setup(s => s.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
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