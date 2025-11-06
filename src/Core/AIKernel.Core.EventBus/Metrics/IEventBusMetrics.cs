namespace AIKernel.Core.EventBus.Metrics;

/// <summary>
/// Interface for event bus metrics collection.
/// </summary>
public interface IEventBusMetrics
{
    /// <summary>
    /// Records a successful event publish.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="latencyMs">The publish latency in milliseconds.</param>
    /// <param name="messageSizeBytes">The message size in bytes.</param>
    void RecordPublish(string topic, double latencyMs, int messageSizeBytes);

    /// <summary>
    /// Records a successful event consume.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="latencyMs">The consume latency in milliseconds.</param>
    /// <param name="messageSizeBytes">The message size in bytes.</param>
    void RecordConsume(string topic, double latencyMs, int messageSizeBytes);

    /// <summary>
    /// Records an error.
    /// </summary>
    /// <param name="component">The component name (producer, consumer, dlq, etc.).</param>
    /// <param name="errorMessage">The error message.</param>
    void RecordError(string component, string errorMessage);

    /// <summary>
    /// Records a dead letter event.
    /// </summary>
    /// <param name="topic">The original topic name.</param>
    void RecordDeadLetter(string topic);

    /// <summary>
    /// Gets a snapshot of current metrics.
    /// </summary>
    /// <returns>A dictionary of metric names to values.</returns>
    Dictionary<string, object> GetSnapshot();
}
