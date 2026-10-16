using Boilerate.Application.Common.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Boilerate.Infrastructure.Caching;

public class LocalCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocalCacheService> _logger;

    public LocalCacheService(IMemoryCache cache, ILogger<LocalCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public T? Get<T>(string key) =>
        _cache.Get<T>(key);

    public Task<T?> GetAsync<T>(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(Get<T>(key));
    }

    public void Refresh(string key) =>
        _cache.TryGetValue(key, out _);

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Refresh(key);
        return Task.CompletedTask;
    }

    public void Remove(string key) =>
        _cache.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Remove(key);
        return Task.CompletedTask;
    }

    public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null)
    {
        slidingExpiration ??= TimeSpan.FromMinutes(10);

        _cache.Set(key, value, new MemoryCacheEntryOptions { SlidingExpiration = slidingExpiration });

        _logger.LogDebug("Added to cache: {CacheKey}", key);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Set(key, value, slidingExpiration);
        return Task.CompletedTask;
    }
}
