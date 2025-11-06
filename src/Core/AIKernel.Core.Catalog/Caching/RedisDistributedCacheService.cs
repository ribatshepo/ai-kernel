using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AIKernel.Core.Catalog.Caching;

public class RedisDistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ICacheMetricsCollector _metricsCollector;
    private readonly ILogger<RedisDistributedCacheService> _logger;
    private readonly CacheDefaultsConfiguration _config;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public RedisDistributedCacheService(
        IDistributedCache cache,
        IConnectionMultiplexer connectionMultiplexer,
        ICacheMetricsCollector metricsCollector,
        ILogger<RedisDistributedCacheService> logger,
        CacheDefaultsConfiguration config)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        var stopwatch = Stopwatch.StartNew();
        var operation = GetOperationName(key);

        try
        {
            var cachedBytes = await _cache.GetAsync(key, cancellationToken);

            if (cachedBytes == null || cachedBytes.Length == 0)
            {
                _metricsCollector.RecordCacheMiss(operation);
                return null;
            }

            var decompressedBytes = _config.EnableCompression
                ? await DecompressAsync(cachedBytes, cancellationToken)
                : cachedBytes;

            var result = JsonSerializer.Deserialize<T>(decompressedBytes, SerializerOptions);

            _metricsCollector.RecordCacheHit(operation);
            _metricsCollector.RecordCacheLatency(operation, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            _metricsCollector.RecordCacheError(operation, ex);
            _logger.LogError(ex, "Failed to retrieve cache entry for key: {CacheKey}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var stopwatch = Stopwatch.StartNew();
        var operation = GetOperationName(key);

        try
        {
            var serializedBytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);

            var bytesToCache = _config.EnableCompression && serializedBytes.Length > _config.CompressionThresholdBytes
                ? await CompressAsync(serializedBytes, cancellationToken)
                : serializedBytes;

            var options = new DistributedCacheEntryOptions();

            if (absoluteExpiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = absoluteExpiration.Value;
            }

            await _cache.SetAsync(key, bytesToCache, options, cancellationToken);

            _metricsCollector.RecordCacheWrite(operation, bytesToCache.Length);
            _metricsCollector.RecordCacheLatency(operation, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _metricsCollector.RecordCacheError(operation, ex);
            _logger.LogError(ex, "Failed to set cache entry for key: {CacheKey}", key);
            throw;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        var operation = GetOperationName(key);

        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _metricsCollector.RecordCacheEviction(operation);
        }
        catch (Exception ex)
        {
            _metricsCollector.RecordCacheError(operation, ex);
            _logger.LogError(ex, "Failed to remove cache entry for key: {CacheKey}", key);
            throw;
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Cache pattern cannot be null or whitespace.", nameof(pattern));

        try
        {
            var database = _connectionMultiplexer.GetDatabase();
            var endpoints = _connectionMultiplexer.GetEndPoints();

            var keysToDelete = new List<RedisKey>();

            foreach (var endpoint in endpoints)
            {
                var server = _connectionMultiplexer.GetServer(endpoint);

                if (server.IsReplica)
                    continue;

                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    keysToDelete.Add(key);

                    if (keysToDelete.Count >= 1000)
                    {
                        await database.KeyDeleteAsync(keysToDelete.ToArray());
                        _metricsCollector.RecordCacheEviction($"pattern:{pattern}");
                        keysToDelete.Clear();
                    }
                }
            }

            if (keysToDelete.Count > 0)
            {
                await database.KeyDeleteAsync(keysToDelete.ToArray());
                _metricsCollector.RecordCacheEviction($"pattern:{pattern}");
            }

            _logger.LogInformation(
                "Removed cache entries matching pattern: {Pattern}",
                pattern);
        }
        catch (Exception ex)
        {
            _metricsCollector.RecordCacheError($"remove_pattern:{pattern}", ex);
            _logger.LogError(ex, "Failed to remove cache entries by pattern: {Pattern}", pattern);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        try
        {
            var database = _connectionMultiplexer.GetDatabase();
            return await database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check cache key existence: {CacheKey}", key);
            return false;
        }
    }

    private static async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken)
    {
        using var outputStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Fastest))
        {
            await gzipStream.WriteAsync(data, cancellationToken);
        }
        return outputStream.ToArray();
    }

    private static async Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken)
    {
        using var inputStream = new MemoryStream(compressedData);
        using var outputStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
        {
            await gzipStream.CopyToAsync(outputStream, cancellationToken);
        }
        return outputStream.ToArray();
    }

    private static string GetOperationName(string key)
    {
        var parts = key.Split(':');
        return parts.Length >= 3 ? parts[2] : "unknown";
    }
}
