using Google.Protobuf;
using StackExchange.Redis;

namespace DungeonServer.Infrastructure.Caching.Generic;

public class RedisProtoCacheService : IProtoCacheService
{
    private readonly IDatabase _redisDatabase;

    public RedisProtoCacheService(IConnectionMultiplexer redis)
    {
        _redisDatabase = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, MessageParser<T> parser, CancellationToken ct = default)
        where T : IMessage<T>
    {
        RedisValue cachedValue = await _redisDatabase.StringGetAsync(key);

        if (cachedValue.IsNull)
        {
            return default;
        }

        try
        {
            var data = (byte[])cachedValue!;
            return parser.ParseFrom(data);
        }
        catch (InvalidCastException)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        where T : IMessage<T>
    {
        await _redisDatabase.StringSetAsync(key, value.ToByteArray(), expiry, When.Always);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _redisDatabase.KeyDeleteAsync(key);
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        MessageParser<T> parser,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : IMessage<T>
    {
        T? cached = await GetAsync(key, parser, ct);

        if (cached != null)
        {
            return cached;
        }

        T queriedValue = await factory();

        await SetAsync(key, queriedValue, expiry, ct);

        return queriedValue;
    }
}