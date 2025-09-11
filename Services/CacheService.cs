using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Generic caching service implementation for performance optimization
    /// Provides thread-safe caching with configurable expiration
    /// </summary>
    /// <typeparam name="TKey">Type of cache keys</typeparam>
    /// <typeparam name="TValue">Type of cached values</typeparam>
    public class CacheService<TKey, TValue> : ICacheService<TKey, TValue>, IDisposable where TKey : notnull
    {
        private readonly ILogger<CacheService<TKey, TValue>> _logger;
        private readonly ConcurrentDictionary<TKey, CacheItem<TValue>> _cache;
        private readonly Timer _cleanupTimer;
        private readonly CacheStatistics _statistics;
        private readonly object _statsLock = new object();
        private readonly TimeSpan _defaultExpiration;
        private bool _disposed;

        public CacheService(ILogger<CacheService<TKey, TValue>> logger, TimeSpan? defaultExpiration = null)
        {
            _logger = logger;
            _cache = new ConcurrentDictionary<TKey, CacheItem<TValue>>();
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(30);
            _statistics = new CacheStatistics();

            // Setup cleanup timer to run every 5 minutes
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _logger.LogDebug("CacheService initialized with default expiration: {Expiration}", _defaultExpiration);
        }

        /// <summary>
        /// Get cached value or create it using factory function
        /// </summary>
        public async Task<TValue> GetOrCreateAsync(TKey key, Func<Task<TValue>> factory, TimeSpan? expiration = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (_disposed)
                throw new ObjectDisposedException(nameof(CacheService<TKey, TValue>));

            var startTime = DateTime.UtcNow;

            // Try to get from cache first
            if (_cache.TryGetValue(key, out var cacheItem) && !cacheItem.IsExpired)
            {
                lock (_statsLock)
                {
                    _statistics.HitCount++;
                    _statistics.LastAccess = DateTime.UtcNow;
                    _statistics.AverageAccessTime = TimeSpan.FromTicks(
                        (_statistics.AverageAccessTime.Ticks + (DateTime.UtcNow - startTime).Ticks) / 2);
                }

                _logger.LogTrace("Cache hit for key: {Key}", key);
                return cacheItem.Value;
            }

            // Cache miss, create value
            lock (_statsLock)
            {
                _statistics.MissCount++;
            }

            _logger.LogTrace("Cache miss for key: {Key}, creating value", key);

            try
            {
                var value = await factory();
                var expirationTime = expiration ?? _defaultExpiration;
                
                Set(key, value, expirationTime);
                
                lock (_statsLock)
                {
                    _statistics.LastAccess = DateTime.UtcNow;
                    _statistics.AverageAccessTime = TimeSpan.FromTicks(
                        (_statistics.AverageAccessTime.Ticks + (DateTime.UtcNow - startTime).Ticks) / 2);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating cached value for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Get cached value synchronously
        /// </summary>
        public TValue? Get(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (_disposed)
                return default;

            if (_cache.TryGetValue(key, out var cacheItem) && !cacheItem.IsExpired)
            {
                lock (_statsLock)
                {
                    _statistics.HitCount++;
                    _statistics.LastAccess = DateTime.UtcNow;
                }

                _logger.LogTrace("Cache hit for key: {Key}", key);
                return cacheItem.Value;
            }

            lock (_statsLock)
            {
                _statistics.MissCount++;
            }

            _logger.LogTrace("Cache miss for key: {Key}", key);
            return default;
        }

        /// <summary>
        /// Set cached value
        /// </summary>
        public void Set(TKey key, TValue value, TimeSpan? expiration = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (_disposed)
                return;

            var expirationTime = expiration ?? _defaultExpiration;
            var cacheItem = new CacheItem<TValue>
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.Add(expirationTime),
                CreatedAt = DateTime.UtcNow
            };

            _cache.AddOrUpdate(key, cacheItem, (k, existing) => cacheItem);

            lock (_statsLock)
            {
                _statistics.CachedItemsCount = _cache.Count;
                _statistics.TotalMemoryUsage = EstimateMemoryUsage();
            }

            _logger.LogTrace("Cached value for key: {Key} with expiration: {Expiration}", key, expirationTime);
        }

        /// <summary>
        /// Remove specific key from cache
        /// </summary>
        public void Invalidate(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (_disposed)
                return;

            if (_cache.TryRemove(key, out _))
            {
                lock (_statsLock)
                {
                    _statistics.CachedItemsCount = _cache.Count;
                    _statistics.TotalMemoryUsage = EstimateMemoryUsage();
                }

                _logger.LogTrace("Invalidated cache key: {Key}", key);
            }
        }

        /// <summary>
        /// Clear all cached items
        /// </summary>
        public void Clear()
        {
            if (_disposed)
                return;

            var count = _cache.Count;
            _cache.Clear();

            lock (_statsLock)
            {
                _statistics.CachedItemsCount = 0;
                _statistics.TotalMemoryUsage = 0;
            }

            _logger.LogDebug("Cleared {Count} items from cache", count);
        }

        /// <summary>
        /// Check if key exists in cache
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (_disposed)
                return false;

            if (_cache.TryGetValue(key, out var cacheItem))
            {
                if (!cacheItem.IsExpired)
                {
                    return true;
                }
                else
                {
                    // Remove expired item
                    _cache.TryRemove(key, out _);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                return new CacheStatistics
                {
                    HitCount = _statistics.HitCount,
                    MissCount = _statistics.MissCount,
                    CachedItemsCount = _statistics.CachedItemsCount,
                    TotalMemoryUsage = _statistics.TotalMemoryUsage,
                    LastAccess = _statistics.LastAccess,
                    AverageAccessTime = _statistics.AverageAccessTime
                };
            }
        }

        private void CleanupExpiredItems(object? state)
        {
            if (_disposed)
                return;

            var expiredKeys = new List<TKey>();
            var now = DateTime.UtcNow;

            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            var removedCount = 0;
            foreach (var key in expiredKeys)
            {
                if (_cache.TryRemove(key, out _))
                {
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                lock (_statsLock)
                {
                    _statistics.CachedItemsCount = _cache.Count;
                    _statistics.TotalMemoryUsage = EstimateMemoryUsage();
                }

                _logger.LogDebug("Cleanup removed {Count} expired cache items", removedCount);
            }
        }

        private long EstimateMemoryUsage()
        {
            // Rough estimation: each cache item ~100 bytes + value size estimation
            var baseSize = _cache.Count * 100;
            
            // For strings, estimate based on character count
            if (typeof(TValue) == typeof(string))
            {
                var stringSize = 0;
                foreach (var item in _cache.Values)
                {
                    if (item.Value is string str)
                    {
                        stringSize += str.Length * 2; // Unicode characters
                    }
                }
                return baseSize + stringSize;
            }

            // For other types, use a rough estimate
            return baseSize + (_cache.Count * 1024); // Assume 1KB per value
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.LogDebug("Disposing CacheService with {Count} items", _cache.Count);

            _cleanupTimer?.Dispose();
            
            var finalStats = GetStatistics();
            _logger.LogInformation(
                "CacheService disposed. Final stats - Hits: {Hits}, Misses: {Misses}, Hit Ratio: {HitRatio:P2}, Items: {Items}, Memory: {Memory} bytes",
                finalStats.HitCount, finalStats.MissCount, finalStats.HitRatio, 
                finalStats.CachedItemsCount, finalStats.TotalMemoryUsage);

            Clear();
            _disposed = true;
        }

        private class CacheItem<T>
        {
            public T Value { get; set; } = default!;
            public DateTime ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; }
            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }
    }
}
