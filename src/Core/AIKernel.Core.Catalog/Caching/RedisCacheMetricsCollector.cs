using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AIKernel.Core.Catalog.Caching;

public class RedisCacheMetricsCollector : ICacheMetricsCollector
{
    private readonly ILogger<RedisCacheMetricsCollector> _logger;
    private readonly MetricsConfiguration _config;

    private long _totalHits;
    private long _totalMisses;
    private long _totalWrites;
    private long _totalEvictions;
    private long _totalErrors;
    private long _totalBytesWritten;

    private readonly ConcurrentDictionary<string, long> _hitsByOperation = new();
    private readonly ConcurrentDictionary<string, long> _missesByOperation = new();
    private readonly ConcurrentDictionary<string, LatencyStats> _latencyByOperation = new();
    private readonly ConcurrentDictionary<string, long> _errorsByOperation = new();

    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    public RedisCacheMetricsCollector(
        ILogger<RedisCacheMetricsCollector> logger,
        MetricsConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void RecordCacheHit(string operation, string? resourceType = null)
    {
        Interlocked.Increment(ref _totalHits);

        var key = BuildOperationKey(operation, resourceType);
        _hitsByOperation.AddOrUpdate(key, 1, (_, count) => count + 1);
    }

    public void RecordCacheMiss(string operation, string? resourceType = null)
    {
        Interlocked.Increment(ref _totalMisses);

        var key = BuildOperationKey(operation, resourceType);
        _missesByOperation.AddOrUpdate(key, 1, (_, count) => count + 1);
    }

    public void RecordCacheWrite(string operation, long sizeBytes)
    {
        Interlocked.Increment(ref _totalWrites);
        Interlocked.Add(ref _totalBytesWritten, sizeBytes);
    }

    public void RecordCacheEviction(string operation)
    {
        Interlocked.Increment(ref _totalEvictions);
    }

    public void RecordCacheError(string operation, Exception exception)
    {
        Interlocked.Increment(ref _totalErrors);

        _errorsByOperation.AddOrUpdate(operation, 1, (_, count) => count + 1);

        _logger.LogError(
            exception,
            "Cache operation '{Operation}' failed. Total errors: {TotalErrors}",
            operation,
            _totalErrors);
    }

    public void RecordCacheLatency(string operation, TimeSpan latency)
    {
        if (!_config.EnableDetailedMetrics)
            return;

        _latencyByOperation.AddOrUpdate(
            operation,
            new LatencyStats { TotalMs = latency.TotalMilliseconds, Count = 1 },
            (_, stats) =>
            {
                stats.TotalMs += latency.TotalMilliseconds;
                stats.Count++;
                return stats;
            });
    }

    public CacheMetricsSnapshot GetSnapshot()
    {
        var totalRequests = _totalHits + _totalMisses;
        var hitRate = totalRequests > 0 ? (double)_totalHits / totalRequests : 0.0;

        var snapshot = new CacheMetricsSnapshot
        {
            TotalHits = _totalHits,
            TotalMisses = _totalMisses,
            TotalWrites = _totalWrites,
            TotalEvictions = _totalEvictions,
            TotalErrors = _totalErrors,
            HitRate = hitRate,
            TotalBytesWritten = _totalBytesWritten,
            SnapshotTime = DateTime.UtcNow
        };

        foreach (var kvp in _hitsByOperation)
        {
            snapshot.HitsByOperation[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in _missesByOperation)
        {
            snapshot.MissesByOperation[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in _latencyByOperation)
        {
            var avgLatencyMs = kvp.Value.Count > 0
                ? kvp.Value.TotalMs / kvp.Value.Count
                : 0;
            snapshot.AverageLatencyByOperation[kvp.Key] = TimeSpan.FromMilliseconds(avgLatencyMs);
        }

        return snapshot;
    }

    public Task PublishMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            return Task.CompletedTask;

        var snapshot = GetSnapshot();

        _logger.LogInformation(
            "Cache Metrics - Uptime: {Uptime}, Hit Rate: {HitRate:P2}, " +
            "Hits: {Hits}, Misses: {Misses}, Writes: {Writes}, " +
            "Evictions: {Evictions}, Errors: {Errors}, Bytes Written: {BytesWritten:N0}",
            _uptime.Elapsed,
            snapshot.HitRate,
            snapshot.TotalHits,
            snapshot.TotalMisses,
            snapshot.TotalWrites,
            snapshot.TotalEvictions,
            snapshot.TotalErrors,
            snapshot.TotalBytesWritten);

        if (_config.EnableDetailedMetrics)
        {
            LogDetailedMetrics(snapshot);
        }

        return Task.CompletedTask;
    }

    private void LogDetailedMetrics(CacheMetricsSnapshot snapshot)
    {
        if (snapshot.HitsByOperation.Any())
        {
            _logger.LogInformation("Cache Hits by Operation:");
            foreach (var kvp in snapshot.HitsByOperation.OrderByDescending(x => x.Value).Take(10))
            {
                _logger.LogInformation("  {Operation}: {Count:N0}", kvp.Key, kvp.Value);
            }
        }

        if (snapshot.MissesByOperation.Any())
        {
            _logger.LogInformation("Cache Misses by Operation:");
            foreach (var kvp in snapshot.MissesByOperation.OrderByDescending(x => x.Value).Take(10))
            {
                _logger.LogInformation("  {Operation}: {Count:N0}", kvp.Key, kvp.Value);
            }
        }

        if (snapshot.AverageLatencyByOperation.Any())
        {
            _logger.LogInformation("Average Cache Latency by Operation:");
            foreach (var kvp in snapshot.AverageLatencyByOperation.OrderByDescending(x => x.Value).Take(10))
            {
                _logger.LogInformation("  {Operation}: {Latency:N2}ms", kvp.Key, kvp.Value.TotalMilliseconds);
            }
        }

        if (_errorsByOperation.Any())
        {
            _logger.LogWarning("Cache Errors by Operation:");
            foreach (var kvp in _errorsByOperation.OrderByDescending(x => x.Value))
            {
                _logger.LogWarning("  {Operation}: {Count:N0}", kvp.Key, kvp.Value);
            }
        }
    }

    private static string BuildOperationKey(string operation, string? resourceType)
    {
        return string.IsNullOrWhiteSpace(resourceType)
            ? operation
            : $"{operation}:{resourceType}";
    }

    private class LatencyStats
    {
        public double TotalMs { get; set; }
        public long Count { get; set; }
    }
}
