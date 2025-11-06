using AIKernel.Core.EventBus.Configuration;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIKernel.Core.EventBus.Health;

/// <summary>
/// Health check for Schema Registry connectivity.
/// </summary>
public class SchemaRegistryHealthCheck : IHealthCheck
{
    private readonly SchemaRegistryConfiguration _configuration;
    private readonly ILogger<SchemaRegistryHealthCheck> _logger;

    public SchemaRegistryHealthCheck(
        IOptions<EventBusConfiguration> configuration,
        ILogger<SchemaRegistryHealthCheck> logger)
    {
        _configuration = configuration.Value.SchemaRegistry;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = new SchemaRegistryConfig
            {
                Url = _configuration.Url,
                BasicAuthUserInfo = _configuration.BasicAuthUserInfo,
                RequestTimeoutMs = 5000
            };

            using var client = new CachedSchemaRegistryClient(config);

            // Try to get all subjects to verify connectivity
            var subjects = await client.GetAllSubjectsAsync();

            var data = new Dictionary<string, object>
            {
                ["url"] = _configuration.Url,
                ["subject_count"] = subjects?.Count ?? 0,
                ["max_cached_schemas"] = _configuration.MaxCachedSchemas
            };

            _logger.LogDebug(
                "Schema Registry health check passed. Subjects: {SubjectCount}",
                subjects?.Count ?? 0);

            return HealthCheckResult.Healthy(
                $"Schema Registry is healthy. {subjects?.Count ?? 0} subject(s) registered.",
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema Registry health check failed");

            return HealthCheckResult.Unhealthy(
                "Schema Registry is unhealthy",
                ex,
                new Dictionary<string, object>
                {
                    ["url"] = _configuration.Url,
                    ["error"] = ex.Message
                });
        }
    }
}
