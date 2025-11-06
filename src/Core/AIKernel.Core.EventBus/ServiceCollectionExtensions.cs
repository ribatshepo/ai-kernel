using AIKernel.Core.EventBus.Configuration;
using AIKernel.Core.EventBus.Consumers;
using AIKernel.Core.EventBus.Health;
using AIKernel.Core.EventBus.Metrics;
using AIKernel.Core.EventBus.Producers;
using AIKernel.Core.EventBus.SchemaRegistry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace AIKernel.Core.EventBus;

/// <summary>
/// Extension methods for registering event bus services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds event bus services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<EventBusConfiguration>(
            configuration.GetSection(EventBusConfiguration.SectionName));

        // Register core services
        services.AddSingleton<IEventHandlerRegistry, EventHandlerRegistry>();
        services.AddSingleton<IEventBusMetrics, EventBusMetricsCollector>();
        services.AddSingleton<IEventProducer, KafkaEventProducer>();
        services.AddSingleton<ISchemaRegistry, ConfluentSchemaRegistry>();
        services.AddSingleton<IDeadLetterQueueHandler, DeadLetterQueueHandler>();
        services.AddSingleton<IEventConsumer, KafkaEventConsumer>();

        // Register health checks
        services.AddHealthChecks()
            .AddCheck<KafkaHealthCheck>(
                name: "kafka",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "eventbus", "kafka", "ready" })
            .AddCheck<SchemaRegistryHealthCheck>(
                name: "schema-registry",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "eventbus", "schema-registry", "ready" });

        return services;
    }

    /// <summary>
    /// Adds event bus services with a custom configuration action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">The configuration action.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services,
        Action<EventBusConfiguration> configureOptions)
    {
        // Register configuration
        services.Configure(configureOptions);

        // Register core services
        services.AddSingleton<IEventHandlerRegistry, EventHandlerRegistry>();
        services.AddSingleton<IEventBusMetrics, EventBusMetricsCollector>();
        services.AddSingleton<IEventProducer, KafkaEventProducer>();
        services.AddSingleton<ISchemaRegistry, ConfluentSchemaRegistry>();
        services.AddSingleton<IDeadLetterQueueHandler, DeadLetterQueueHandler>();
        services.AddSingleton<IEventConsumer, KafkaEventConsumer>();

        // Register health checks
        services.AddHealthChecks()
            .AddCheck<KafkaHealthCheck>(
                name: "kafka",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "eventbus", "kafka", "ready" })
            .AddCheck<SchemaRegistryHealthCheck>(
                name: "schema-registry",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "eventbus", "schema-registry", "ready" });

        return services;
    }

    /// <summary>
    /// Registers an event handler for a specific event type.
    /// </summary>
    /// <typeparam name="TData">The event data type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEventHandler<TData, THandler>(
        this IServiceCollection services)
        where TData : class
        where THandler : class, IEventHandler<TData>
    {
        // Register the handler in DI as scoped (new instance per event)
        services.AddScoped<THandler>();

        // Register via a hosted service that will register the handler with the registry on startup
        services.AddHostedService<EventHandlerRegistrationService<TData, THandler>>();

        return services;
    }

    private class EventHandlerRegistrationService<TData, THandler> : IHostedService
        where TData : class
        where THandler : class, IEventHandler<TData>
    {
        private readonly IEventHandlerRegistry _registry;

        public EventHandlerRegistrationService(IEventHandlerRegistry registry)
        {
            _registry = registry;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _registry.Register<TData, THandler>();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
