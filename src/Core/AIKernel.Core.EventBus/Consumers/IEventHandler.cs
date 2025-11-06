using AIKernel.Core.EventBus.Models;

namespace AIKernel.Core.EventBus.Consumers;

/// <summary>
/// Interface for handling events of a specific type.
/// </summary>
/// <typeparam name="TData">The type of event data to handle.</typeparam>
public interface IEventHandler<TData> where TData : class
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="envelope">The event envelope containing the event and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the handling operation.</returns>
    Task HandleAsync(
        EventEnvelope<TData> envelope,
        CancellationToken cancellationToken = default);
}
