using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace MCPConvert.Services
{
    /// <summary>
    /// Service for tracking API usage and quota to manage Azure Free Tier resource constraints
    /// </summary>
    public class UsageTrackingService
    {
        private readonly ILogger<UsageTrackingService> _logger;
        private readonly ConcurrentDictionary<string, int> _dailyClientUsage = new();
        private readonly ConcurrentDictionary<DateOnly, int> _dailyTotalUsage = new();
        
        // Track total conversions and processing time
        private long _totalConversions = 0;
        private long _totalProcessingTimeMs = 0;
        
        // Estimated quota limits for Azure Free Tier
        private readonly int _dailyQuotaLimit = 500; // Estimated number of conversions per day
        private readonly int _clientQuotaLimit = 50; // Limit per client (IP) per day
        
        // Date of the last reset
        private DateOnly _lastResetDate = DateOnly.FromDateTime(DateTime.UtcNow);

        /// <summary>
        /// Initializes a new instance of the <see cref="UsageTrackingService"/> class
        /// </summary>
        /// <param name="logger">Logger</param>
        public UsageTrackingService(ILogger<UsageTrackingService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Records a conversion for usage tracking
        /// </summary>
        /// <param name="clientId">Client identifier (e.g., IP address)</param>
        /// <param name="processingTimeMs">Processing time in milliseconds</param>
        public void RecordConversion(string clientId, long processingTimeMs)
        {
            // Check if we need to reset daily counters
            CheckAndResetDaily();
            
            // Increment total conversions
            Interlocked.Increment(ref _totalConversions);
            
            // Add processing time
            Interlocked.Add(ref _totalProcessingTimeMs, processingTimeMs);
            
            // Record client usage
            _dailyClientUsage.AddOrUpdate(clientId, 1, (_, count) => count + 1);
            
            // Record daily total
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            _dailyTotalUsage.AddOrUpdate(today, 1, (_, count) => count + 1);
            
            _logger.LogInformation("Recorded conversion for client {ClientId}, processing time {ProcessingTimeMs}ms", 
                clientId, processingTimeMs);
        }

        /// <summary>
        /// Checks if a client has exceeded their daily quota
        /// </summary>
        /// <param name="clientId">Client identifier (e.g., IP address)</param>
        /// <returns>True if quota exceeded, false otherwise</returns>
        public bool IsClientQuotaExceeded(string clientId)
        {
            CheckAndResetDaily();
            
            return _dailyClientUsage.TryGetValue(clientId, out var count) && count >= _clientQuotaLimit;
        }

        /// <summary>
        /// Checks if the total daily quota has been exceeded
        /// </summary>
        /// <returns>True if quota exceeded, false otherwise</returns>
        public bool IsDailyQuotaExceeded()
        {
            CheckAndResetDaily();
            
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return _dailyTotalUsage.TryGetValue(today, out var count) && count >= _dailyQuotaLimit;
        }

        /// <summary>
        /// Gets usage statistics
        /// </summary>
        /// <returns>Usage statistics</returns>
        public UsageStatistics GetStatistics()
        {
            CheckAndResetDaily();
            
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            _dailyTotalUsage.TryGetValue(today, out var dailyCount);
            
            return new UsageStatistics
            {
                TotalConversions = Interlocked.Read(ref _totalConversions),
                TotalProcessingTimeMs = Interlocked.Read(ref _totalProcessingTimeMs),
                DailyConversions = dailyCount,
                DailyQuotaLimit = _dailyQuotaLimit,
                DailyQuotaPercentage = (double)dailyCount / _dailyQuotaLimit * 100,
                DailyQuotaRemaining = Math.Max(0, _dailyQuotaLimit - dailyCount)
            };
        }

        private void CheckAndResetDaily()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            if (today > _lastResetDate)
            {
                _logger.LogInformation("Resetting daily usage counters");
                _dailyClientUsage.Clear();
                _lastResetDate = today;
            }
        }
    }

    /// <summary>
    /// Statistics about API usage
    /// </summary>
    public class UsageStatistics
    {
        /// <summary>
        /// Total number of conversions since service start
        /// </summary>
        public long TotalConversions { get; set; }

        /// <summary>
        /// Total processing time in milliseconds since service start
        /// </summary>
        public long TotalProcessingTimeMs { get; set; }

        /// <summary>
        /// Number of conversions today
        /// </summary>
        public int DailyConversions { get; set; }

        /// <summary>
        /// Daily quota limit
        /// </summary>
        public int DailyQuotaLimit { get; set; }

        /// <summary>
        /// Percentage of daily quota used
        /// </summary>
        public double DailyQuotaPercentage { get; set; }

        /// <summary>
        /// Number of conversions remaining in daily quota
        /// </summary>
        public int DailyQuotaRemaining { get; set; }
    }
}
