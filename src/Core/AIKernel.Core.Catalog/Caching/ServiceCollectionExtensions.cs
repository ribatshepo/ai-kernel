using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AIKernel.Core.Catalog.Caching;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cachingSection = configuration.GetSection(CachingConfiguration.SectionName);
        services.Configure<CachingConfiguration>(cachingSection);

        services.AddSingleton<IValidateOptions<CachingConfiguration>, CachingConfigurationValidator>();

        var cachingConfig = cachingSection.Get<CachingConfiguration>()
            ?? throw new InvalidOperationException("Caching configuration is not properly configured");

        services.AddSingleton(cachingConfig.Redis);
        services.AddSingleton(cachingConfig.Defaults);
        services.AddSingleton(cachingConfig.Metrics);

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisConfig = sp.GetRequiredService<RedisConfiguration>();

            var configOptions = ConfigurationOptions.Parse(redisConfig.ConnectionString);
            configOptions.ConnectTimeout = redisConfig.ConnectTimeout;
            configOptions.SyncTimeout = redisConfig.SyncTimeout;
            configOptions.ConnectRetry = redisConfig.ConnectRetry;
            configOptions.AbortOnConnectFail = redisConfig.AbortOnConnectFail;

            if (redisConfig.UseSsl)
            {
                configOptions.Ssl = true;
                if (!string.IsNullOrWhiteSpace(redisConfig.SslHost))
                {
                    configOptions.SslHost = redisConfig.SslHost;
                }
            }

            configOptions.ClientName = $"{redisConfig.InstanceName}_catalog";

            return ConnectionMultiplexer.Connect(configOptions);
        });

        services.AddStackExchangeRedisCache(options =>
        {
            var redisConfig = cachingConfig.Redis;
            options.InstanceName = redisConfig.InstanceName;
            options.ConfigurationOptions = ConfigurationOptions.Parse(redisConfig.ConnectionString);
            options.ConfigurationOptions.ConnectTimeout = redisConfig.ConnectTimeout;
            options.ConfigurationOptions.SyncTimeout = redisConfig.SyncTimeout;
            options.ConfigurationOptions.ConnectRetry = redisConfig.ConnectRetry;
            options.ConfigurationOptions.AbortOnConnectFail = redisConfig.AbortOnConnectFail;

            if (redisConfig.UseSsl)
            {
                options.ConfigurationOptions.Ssl = true;
                if (!string.IsNullOrWhiteSpace(redisConfig.SslHost))
                {
                    options.ConfigurationOptions.SslHost = redisConfig.SslHost;
                }
            }
        });

        services.AddSingleton<ICacheKeyGenerator>(sp =>
        {
            var redisConfig = sp.GetRequiredService<RedisConfiguration>();
            return new RedisCacheKeyGenerator(redisConfig.InstanceName);
        });

        services.AddSingleton<ICacheMetricsCollector, RedisCacheMetricsCollector>();
        services.AddSingleton<IDistributedCacheService, RedisDistributedCacheService>();

        services.AddSingleton<IHostedService, CacheMetricsPublisher>();

        services.AddHealthChecks()
            .AddCheck<CacheHealthCheck>(
                "redis_cache",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "cache", "redis", "infrastructure" });

        return services;
    }

    public static IServiceCollection AddCachedRepositories(this IServiceCollection services)
    {
        services.Decorate<AIKernel.Core.Catalog.Contracts.Interfaces.IResourceRepository, CachedResourceRepository>();
        services.Decorate<AIKernel.Core.Catalog.Contracts.Interfaces.IRelationshipRepository, CachedRelationshipRepository>();
        services.Decorate<AIKernel.Core.Catalog.Contracts.Interfaces.ISearchRepository, CachedSearchRepository>();

        return services;
    }
}
