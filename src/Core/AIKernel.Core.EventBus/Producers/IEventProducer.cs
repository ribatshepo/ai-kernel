namespace AIKernel.Core.EventBus.Producers;

/// <summary>
/// Interface for publishing events to the event bus.
/// </summary>
public interface IEventProducer
{
    /// <summary>
    /// Publishes an event to the specified topic.
    /// </summary>
    /// <typeparam name="TData">The type of event data.</typeparam>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="data">The event data.</param>
    /// <param name="partitionKey">Optional partition key for routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The published event ID.</returns>
    Task<string> PublishAsync<TData>(
        string topic,
        TData data,
        string? partitionKey = null,
        CancellationToken cancellationToken = default)
        where TData : class;

    /// <summary>
    /// Publishes multiple events to the specified topic in a batch.
    /// </summary>
    /// <typeparam name="TData">The type of event data.</typeparam>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="events">The collection of event data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection of published event IDs.</returns>
    Task<IEnumerable<string>> PublishBatchAsync<TData>(
        string topic,
        IEnumerable<TData> events,
        CancellationToken cancellationToken = default)
        where TData : class;

    /// <summary>
    /// Flushes any pending messages to the broker.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for flush.</param>
    /// <returns>A task representing the flush operation.</returns>
    Task FlushAsync(TimeSpan? timeout = null);
}
