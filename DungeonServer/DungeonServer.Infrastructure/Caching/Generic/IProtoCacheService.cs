using Google.Protobuf;

namespace DungeonServer.Infrastructure.Caching.Generic;

public interface IProtoCacheService
{
    Task<T?> GetAsync<T>(string key, MessageParser<T> parser, CancellationToken ct = default) where T : IMessage<T>;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : IMessage<T>;

    Task DeleteAsync(string key, CancellationToken ct = default);

    Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        MessageParser<T> parser,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : IMessage<T>;
}