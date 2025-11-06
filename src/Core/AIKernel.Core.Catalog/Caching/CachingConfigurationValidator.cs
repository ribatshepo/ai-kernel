using Microsoft.Extensions.Options;

namespace AIKernel.Core.Catalog.Caching;

public class CachingConfigurationValidator : IValidateOptions<CachingConfiguration>
{
    public ValidateOptionsResult Validate(string? name, CachingConfiguration options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("Caching configuration cannot be null");
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Redis?.ConnectionString))
        {
            errors.Add("Redis connection string is required");
        }

        if (string.IsNullOrWhiteSpace(options.Redis?.InstanceName))
        {
            errors.Add("Redis instance name is required");
        }

        if (options.Redis?.ConnectTimeout <= 0)
        {
            errors.Add("Redis connect timeout must be greater than zero");
        }

        if (options.Redis?.SyncTimeout <= 0)
        {
            errors.Add("Redis sync timeout must be greater than zero");
        }

        if (options.Redis?.ConnectRetry < 0)
        {
            errors.Add("Redis connect retry cannot be negative");
        }

        if (options.Defaults?.ResourceCacheTtlSeconds <= 0)
        {
            errors.Add("Resource cache TTL must be greater than zero");
        }

        if (options.Defaults?.SearchResultsTtlSeconds <= 0)
        {
            errors.Add("Search results cache TTL must be greater than zero");
        }

        if (options.Defaults?.VersionHistoryTtlSeconds <= 0)
        {
            errors.Add("Version history cache TTL must be greater than zero");
        }

        if (options.Defaults?.LineageQueryTtlSeconds <= 0)
        {
            errors.Add("Lineage query cache TTL must be greater than zero");
        }

        if (options.Defaults?.RelationshipCacheTtlSeconds <= 0)
        {
            errors.Add("Relationship cache TTL must be greater than zero");
        }

        if (options.Defaults?.CompressionThresholdBytes < 0)
        {
            errors.Add("Compression threshold cannot be negative");
        }

        if (options.Metrics?.PublishIntervalSeconds <= 0)
        {
            errors.Add("Metrics publish interval must be greater than zero");
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(string.Join("; ", errors));
        }

        return ValidateOptionsResult.Success;
    }
}
