using AIKernel.Core.EventBus.Models;

namespace AIKernel.Core.EventBus.Consumers;

/// <summary>
/// Interface for handling failed events and sending them to the dead letter queue.
/// </summary>
public interface IDeadLetterQueueHandler
{
    /// <summary>
    /// Handles a failed event by sending it to the dead letter queue.
    /// </summary>
    /// <param name="deadLetterEvent">The failed event details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleFailedEventAsync(
        DeadLetterEvent deadLetterEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries processing a dead letter event with exponential backoff.
    /// </summary>
    /// <param name="deadLetterEvent">The dead letter event to retry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if retry was successful, false otherwise.</returns>
    Task<bool> RetryEventAsync(
        DeadLetterEvent deadLetterEvent,
        CancellationToken cancellationToken = default);
}
