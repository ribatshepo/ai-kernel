namespace AIKernel.Core.EventBus.Models;

/// <summary>
/// Represents an event that failed processing and was sent to the dead letter queue.
/// </summary>
public class DeadLetterEvent
{
    /// <summary>
    /// Original event ID.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Original event type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Topic from which the event originated.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Partition number.
    /// </summary>
    public int Partition { get; set; }

    /// <summary>
    /// Offset in the partition.
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// Serialized event payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Error message describing the failure.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Full exception details including stack trace.
    /// </summary>
    public string? ExceptionDetails { get; set; }

    /// <summary>
    /// Number of processing attempts.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Timestamp of the first failure.
    /// </summary>
    public DateTimeOffset FirstFailureAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp of the last failure.
    /// </summary>
    public DateTimeOffset LastFailureAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Consumer group that failed to process the event.
    /// </summary>
    public string ConsumerGroup { get; set; } = string.Empty;

    /// <summary>
    /// Additional diagnostic information.
    /// </summary>
    public Dictionary<string, string> Diagnostics { get; set; } = new();
}
