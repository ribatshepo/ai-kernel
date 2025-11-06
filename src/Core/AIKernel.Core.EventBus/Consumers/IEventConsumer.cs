namespace AIKernel.Core.EventBus.Consumers;

/// <summary>
/// Interface for consuming events from the event bus.
/// </summary>
public interface IEventConsumer
{
    /// <summary>
    /// Starts consuming events from the specified topics.
    /// </summary>
    /// <param name="topics">The topics to consume from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops consuming events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
