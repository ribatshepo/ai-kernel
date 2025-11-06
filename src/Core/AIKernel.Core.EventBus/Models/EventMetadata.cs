namespace AIKernel.Core.EventBus.Models;

/// <summary>
/// Metadata about event processing and routing.
/// </summary>
public class EventMetadata
{
    /// <summary>
    /// Correlation ID for tracing related events across services.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Causation ID linking cause-effect events.
    /// </summary>
    public string? CausationId { get; set; }

    /// <summary>
    /// Tenant identifier for multi-tenancy support.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// User identifier who triggered the event.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Number of times this event has been retried.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Maximum number of retry attempts allowed.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Timestamp when the event was first published.
    /// </summary>
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Priority level for event processing (0=lowest, 10=highest).
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Partition key for Kafka partitioning.
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// Additional custom headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
}
