using System.Text;
using StackExchange.Redis;
using Boilerate.Application.Common.Caching;
using Boilerate.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Boilerate.Infrastructure.Caching;

public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly ISerializerService _serializer;

    public DistributedCacheService(IDistributedCache cache, ISerializerService serializer, ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _serializer = serializer;
        _logger = logger;
    }

    public T? Get<T>(string key) =>
        Get(key) is { } data ? Deserialize<T>(key, data) : default;

    private byte[]? Get(string key)
    {
        try
        {
            return _cache.Get(key);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            _logger.LogError(ex, "Error while getting from cache.");
            return null;
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken token = default) =>
        await GetAsync(key, token) is { } data ? Deserialize<T>(key, data) : default;

    private async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        try
        {
            return await _cache.GetAsync(key, token);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && IsUnavailable(ex))
        {
            _logger.LogError(ex, "Error while getting from cache async.");
            return null;
        }
    }

    public void Refresh(string key)
    {
        try
        {
            _cache.Refresh(key);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            _logger.LogWarning(ex, "Optional cache operation failed; continuing without cache.");
        }
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        try
        {
            await _cache.RefreshAsync(key, token);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && IsUnavailable(ex))
        {
            _logger.LogWarning(ex, "Optional cache operation failed; continuing without cache.");
        }
    }

    public void Remove(string key)
    {
        try
        {
            _cache.Remove(key);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            _logger.LogWarning(ex, "Optional cache operation failed; continuing without cache.");
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        try
        {
            await _cache.RemoveAsync(key, token);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && IsUnavailable(ex))
        {
            _logger.LogWarning(ex, "Optional cache operation failed; continuing without cache.");
        }
    }

    public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null) =>
        Set(key, Serialize(value), slidingExpiration);

    private void Set(string key, byte[] value, TimeSpan? slidingExpiration = null)
    {
        try
        {
            _cache.Set(key, value, GetOptions(slidingExpiration));
            _logger.LogDebug("Added to cache: {CacheKey}", key);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            _logger.LogWarning(ex, "Optional cache operation failed; continuing without cache.");
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SetAsync(key, Serialize(value), slidingExpiration, cancellationToken);
    }

    private async Task SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null, CancellationToken token = default)
    {
        try
        {
            await _cache.SetAsync(key, value, GetOptions(slidingExpiration), token);
            _logger.LogDebug("Added to cache: {CacheKey}", key);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && IsUnavailable(ex))
        {
            _logger.LogWarning(ex, "Optional cache operation failed; continuing without cache.");
        }
    }

    // Cache is an optimization. Infrastructure outages may fail open; application bugs must surface.
    private static bool IsUnavailable(Exception exception) =>
        exception is RedisConnectionException or RedisTimeoutException or TimeoutException or IOException;

    private byte[] Serialize<T>(T item) =>
        Encoding.UTF8.GetBytes(_serializer.Serialize(item));

    private T? Deserialize<T>(string key, byte[] cachedData)
    {
        try
        {
            return _serializer.Deserialize<T>(Encoding.UTF8.GetString(cachedData));
        }
        catch (Exception ex) when (ex is Newtonsoft.Json.JsonException or System.Text.Json.JsonException)
        {
            // Conversion exceptions can embed cached values; retain the type, never their message/payload.
            _logger.LogWarning("Discarding an unreadable cache entry ({ExceptionType}).", ex.GetType().Name);
            Remove(key);
            return default;
        }
    }

    private static DistributedCacheEntryOptions GetOptions(TimeSpan? slidingExpiration)
    {
        var options = new DistributedCacheEntryOptions();
        if (slidingExpiration.HasValue)
        {
            options.SetSlidingExpiration(slidingExpiration.Value);
        }
        else
        {
            options.SetSlidingExpiration(TimeSpan.FromMinutes(10));
        }

        return options;
    }
}
