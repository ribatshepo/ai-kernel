namespace AIKernel.Core.EventBus.Models;

/// <summary>
/// Complete event envelope containing CloudEvent and processing metadata.
/// </summary>
public class EventEnvelope<TData> where TData : class
{
    /// <summary>
    /// CloudEvents 1.0 compliant event data.
    /// </summary>
    public CloudEvent<TData> Event { get; set; } = new();

    /// <summary>
    /// Processing and routing metadata.
    /// </summary>
    public EventMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Schema version for data evolution.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Creates an event envelope from data.
    /// </summary>
    public static EventEnvelope<TData> Create(
        TData data,
        string eventType,
        string source,
        string? subject = null,
        string? correlationId = null,
        string? partitionKey = null)
    {
        return new EventEnvelope<TData>
        {
            Event = new CloudEvent<TData>
            {
                Id = Guid.NewGuid().ToString(),
                Type = eventType,
                Source = source,
                Subject = subject,
                Time = DateTimeOffset.UtcNow,
                Data = data,
                SpecVersion = "1.0",
                DataContentType = "application/json"
            },
            Metadata = new EventMetadata
            {
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                PartitionKey = partitionKey,
                PublishedAt = DateTimeOffset.UtcNow
            }
        };
    }
}
