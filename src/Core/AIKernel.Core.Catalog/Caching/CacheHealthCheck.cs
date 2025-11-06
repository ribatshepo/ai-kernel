using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AIKernel.Core.Catalog.Caching;

public class CacheHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDistributedCacheService _cacheService;

    public CacheHealthCheck(
        IConnectionMultiplexer connectionMultiplexer,
        IDistributedCacheService cacheService)
    {
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_connectionMultiplexer.IsConnected)
            {
                return HealthCheckResult.Unhealthy(
                    "Redis connection is not established",
                    data: new Dictionary<string, object>
                    {
                        ["IsConnected"] = false
                    });
            }

            var database = _connectionMultiplexer.GetDatabase();
            var endpoints = _connectionMultiplexer.GetEndPoints();

            var healthData = new Dictionary<string, object>
            {
                ["IsConnected"] = true,
                ["EndpointCount"] = endpoints.Length
            };

            var pingTasks = endpoints.Select(async endpoint =>
            {
                var server = _connectionMultiplexer.GetServer(endpoint);
                var latency = await database.PingAsync();
                return new { Endpoint = endpoint.ToString(), Latency = latency.TotalMilliseconds, IsConnected = server.IsConnected };
            });

            var results = await Task.WhenAll(pingTasks);

            healthData["Endpoints"] = results.Select(r => new
            {
                r.Endpoint,
                LatencyMs = r.Latency,
                r.IsConnected
            }).ToList();

            var maxLatency = results.Max(r => r.Latency);
            var allConnected = results.All(r => r.IsConnected);

            if (!allConnected)
            {
                return HealthCheckResult.Degraded(
                    "Some Redis endpoints are not connected",
                    data: healthData);
            }

            if (maxLatency > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Redis latency is high: {maxLatency:F2}ms",
                    data: healthData);
            }

            var testKey = $"health_check:{Guid.NewGuid()}";
            var testValue = DateTime.UtcNow.ToString("O");

            await _cacheService.SetAsync(testKey, testValue, TimeSpan.FromSeconds(10), cancellationToken);
            var retrieved = await _cacheService.GetAsync<string>(testKey, cancellationToken);
            await _cacheService.RemoveAsync(testKey, cancellationToken);

            if (retrieved != testValue)
            {
                return HealthCheckResult.Unhealthy(
                    "Cache read/write verification failed",
                    data: healthData);
            }

            return HealthCheckResult.Healthy(
                $"Redis is healthy. Max latency: {maxLatency:F2}ms",
                data: healthData);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Redis health check failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["ExceptionType"] = ex.GetType().Name,
                    ["Message"] = ex.Message
                });
        }
    }
}
