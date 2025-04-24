using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MCPConvert.Models;
using Microsoft.Extensions.Logging;

namespace MCPConvert.Services
{
    /// <summary>
    /// Service for caching conversion results to improve performance and reduce resource usage
    /// </summary>
    public class ConversionCacheService
    {
        private readonly ILogger<ConversionCacheService> _logger;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
        
        // Track cache statistics for monitoring
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        private long _cacheSize = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversionCacheService"/> class
        /// </summary>
        /// <param name="logger">Logger</param>
        public ConversionCacheService(ILogger<ConversionCacheService> logger)
        {
            _logger = logger;
            _cleanupTimer = new Timer(CleanupCache, null, _cleanupInterval, _cleanupInterval);
        }

        /// <summary>
        /// Gets a cached conversion result if available
        /// </summary>
        /// <param name="contentHash">Content hash of the input</param>
        /// <returns>Cached conversion result or null if not found</returns>
        public ConversionResponse? GetCachedResult(string contentHash)
        {
            if (string.IsNullOrEmpty(contentHash))
            {
                return null;
            }

            if (_cache.TryGetValue(contentHash, out var entry) && !IsExpired(entry))
            {
                Interlocked.Increment(ref _cacheHits);
                _logger.LogInformation("Cache hit for content hash: {ContentHash}", contentHash);
                return entry.Response;
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogInformation("Cache miss for content hash: {ContentHash}", contentHash);
            return null;
        }

        /// <summary>
        /// Caches a conversion result
        /// </summary>
        /// <param name="contentHash">Content hash of the input</param>
        /// <param name="response">Conversion response to cache</param>
        public void CacheResult(string contentHash, ConversionResponse response)
        {
            if (string.IsNullOrEmpty(contentHash) || response == null)
            {
                return;
            }

            var entry = new CacheEntry
            {
                Response = response,
                ExpirationTime = DateTime.UtcNow.Add(_cacheExpiration)
            };

            if (_cache.TryAdd(contentHash, entry))
            {
                Interlocked.Increment(ref _cacheSize);
                _logger.LogInformation("Cached result for content hash: {ContentHash}", contentHash);
            }
            else if (_cache.TryUpdate(contentHash, entry, _cache[contentHash]))
            {
                _logger.LogInformation("Updated cached result for content hash: {ContentHash}", contentHash);
            }
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        /// <returns>Cache statistics</returns>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                CacheHits = Interlocked.Read(ref _cacheHits),
                CacheMisses = Interlocked.Read(ref _cacheMisses),
                CacheSize = Interlocked.Read(ref _cacheSize),
                HitRatio = CalculateHitRatio()
            };
        }

        private double CalculateHitRatio()
        {
            long hits = Interlocked.Read(ref _cacheHits);
            long misses = Interlocked.Read(ref _cacheMisses);
            long total = hits + misses;

            return total > 0 ? (double)hits / total : 0;
        }

        private bool IsExpired(CacheEntry entry)
        {
            return entry.ExpirationTime < DateTime.UtcNow;
        }

        private void CleanupCache(object? state)
        {
            int removedCount = 0;
            var now = DateTime.UtcNow;

            foreach (var key in _cache.Keys)
            {
                if (_cache.TryGetValue(key, out var entry) && entry.ExpirationTime < now)
                {
                    if (_cache.TryRemove(key, out _))
                    {
                        Interlocked.Decrement(ref _cacheSize);
                        removedCount++;
                    }
                }
            }

            if (removedCount > 0)
            {
                _logger.LogInformation("Removed {Count} expired cache entries", removedCount);
            }
        }

        /// <summary>
        /// Cache entry with expiration time
        /// </summary>
        private class CacheEntry
        {
            /// <summary>
            /// Cached conversion response
            /// </summary>
            public ConversionResponse Response { get; set; } = null!;

            /// <summary>
            /// When this cache entry expires
            /// </summary>
            public DateTime ExpirationTime { get; set; }
        }
    }

    /// <summary>
    /// Statistics about the conversion cache
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Number of cache hits
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Number of cache misses
        /// </summary>
        public long CacheMisses { get; set; }

        /// <summary>
        /// Current number of items in the cache
        /// </summary>
        public long CacheSize { get; set; }

        /// <summary>
        /// Ratio of hits to total requests (hits + misses)
        /// </summary>
        public double HitRatio { get; set; }
    }
}
