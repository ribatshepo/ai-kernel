using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AIKernel.Core.Catalog.Caching;

public static class SessionStorageExtensions
{
    public static IServiceCollection AddDistributedSessionStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cachingSection = configuration.GetSection(CachingConfiguration.SectionName);
        var cachingConfig = cachingSection.Get<CachingConfiguration>()
            ?? throw new InvalidOperationException("Caching configuration is not properly configured");

        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.Name = ".AIKernel.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        });

        var redis = ConnectionMultiplexer.Connect(cachingConfig.Redis.ConnectionString);
        services.AddDataProtection()
            .PersistKeysToStackExchangeRedis(redis, $"{cachingConfig.Redis.InstanceName}data_protection_keys");

        return services;
    }

    public static IApplicationBuilder UseDistributedSessionStorage(this IApplicationBuilder app)
    {
        app.UseSession();
        return app;
    }
}
