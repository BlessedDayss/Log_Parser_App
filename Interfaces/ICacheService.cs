using System;
using System.Threading.Tasks;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Generic caching service interface for performance optimization
    /// Provides thread-safe caching with configurable expiration
    /// </summary>
    /// <typeparam name="TKey">Type of cache keys</typeparam>
    /// <typeparam name="TValue">Type of cached values</typeparam>
    public interface ICacheService<TKey, TValue> where TKey : notnull
    {
        /// <summary>
        /// Get cached value or create it using factory function
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create value if not cached</param>
        /// <param name="expiration">Optional expiration time</param>
        /// <returns>Cached or newly created value</returns>
        Task<TValue> GetOrCreateAsync(TKey key, Func<Task<TValue>> factory, TimeSpan? expiration = null);

        /// <summary>
        /// Get cached value synchronously
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>Cached value or default</returns>
        TValue? Get(TKey key);

        /// <summary>
        /// Set cached value
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Optional expiration time</param>
        void Set(TKey key, TValue value, TimeSpan? expiration = null);

        /// <summary>
        /// Remove specific key from cache
        /// </summary>
        /// <param name="key">Cache key to remove</param>
        void Invalidate(TKey key);

        /// <summary>
        /// Clear all cached items
        /// </summary>
        void Clear();

        /// <summary>
        /// Check if key exists in cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True if key exists</returns>
        bool ContainsKey(TKey key);

        /// <summary>
        /// Get cache statistics
        /// </summary>
        /// <returns>Cache performance statistics</returns>
        CacheStatistics GetStatistics();
    }

    /// <summary>
    /// Cache performance statistics
    /// </summary>
    public class CacheStatistics
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public double HitRatio => (HitCount + MissCount) > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public int CachedItemsCount { get; set; }
        public long TotalMemoryUsage { get; set; }
        public DateTime LastAccess { get; set; }
        public TimeSpan AverageAccessTime { get; set; }
    }
}

