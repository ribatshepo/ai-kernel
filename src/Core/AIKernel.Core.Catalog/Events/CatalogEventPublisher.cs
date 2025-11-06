using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AIKernel.Core.Catalog.Events;

public interface ICatalogEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : CatalogEvent;
    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : CatalogEvent;
}

/// <summary>
/// In-memory event publisher for catalog events
/// Supports asynchronous event handling with subscriber notifications
/// </summary>
public class CatalogEventPublisher : ICatalogEventPublisher
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();
    private readonly ILogger<CatalogEventPublisher> _logger;

    public CatalogEventPublisher(ILogger<CatalogEventPublisher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Publishes an event to all registered subscribers
    /// </summary>
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : CatalogEvent
    {
        var eventType = typeof(TEvent);
        _logger.LogDebug("Publishing event {EventType} with ID {EventId}", eventType.Name, @event.EventId);

        if (!_subscribers.TryGetValue(eventType, out var handlers))
        {
            _logger.LogDebug("No subscribers for event type {EventType}", eventType.Name);
            return;
        }

        var tasks = new List<Task>();

        foreach (var handler in handlers.ToList())
        {
            var typedHandler = (Func<TEvent, CancellationToken, Task>)handler;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await typedHandler(@event, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event {EventType} with ID {EventId}",
                        eventType.Name, @event.EventId);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);

        _logger.LogDebug("Published event {EventType} to {Count} subscribers", eventType.Name, handlers.Count);
    }

    /// <summary>
    /// Subscribes a handler to a specific event type
    /// </summary>
    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : CatalogEvent
    {
        var eventType = typeof(TEvent);
        _subscribers.AddOrUpdate(
            eventType,
            _ => new List<Delegate> { handler },
            (_, existing) =>
            {
                existing.Add(handler);
                return existing;
            });

        _logger.LogInformation("Subscribed handler to event type {EventType}", eventType.Name);
    }
}

/// <summary>
/// Event synchronization handler that keeps search index in sync with catalog changes
/// </summary>
public class SearchIndexSynchronizer
{
    private readonly ICatalogEventPublisher _eventPublisher;
    private readonly ILogger<SearchIndexSynchronizer> _logger;

    public SearchIndexSynchronizer(
        ICatalogEventPublisher eventPublisher,
        ILogger<SearchIndexSynchronizer> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;

        // Subscribe to catalog events
        _eventPublisher.Subscribe<ResourceCreatedEvent>(OnResourceCreatedAsync);
        _eventPublisher.Subscribe<ResourceUpdatedEvent>(OnResourceUpdatedAsync);
        _eventPublisher.Subscribe<ResourceDeletedEvent>(OnResourceDeletedAsync);
    }

    private async Task OnResourceCreatedAsync(ResourceCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling ResourceCreated event for {ResourceId}", @event.ResourceId);
        // Search index is already updated in CatalogService.RegisterResourceAsync
        // This handler can be used for additional processing like cache invalidation
        await Task.CompletedTask;
    }

    private async Task OnResourceUpdatedAsync(ResourceUpdatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling ResourceUpdated event for {ResourceId}", @event.ResourceId);
        // Search index is already updated in CatalogService.UpdateResourceAsync
        // This handler can be used for additional processing like cache invalidation
        await Task.CompletedTask;
    }

    private async Task OnResourceDeletedAsync(ResourceDeletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling ResourceDeleted event for {ResourceId}", @event.ResourceId);
        // Search index is already updated in CatalogService.DeleteResourceAsync
        // This handler can be used for additional processing like cache invalidation
        await Task.CompletedTask;
    }
}
