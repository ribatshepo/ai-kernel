
using System.Text.Json.Serialization;

namespace AIKernel.Core.EventBus.Models;

/// <summary>
/// CloudEvents 1.0 specification compliant event wrapper.
/// See: https://github.com/cloudevents/spec/blob/v1.0/spec.md
/// </summary>
public class CloudEvent<TData> where TData : class
{
    /// <summary>
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct event.
    /// REQUIRED.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Identifies the context in which an event happened.
    /// REQUIRED.
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// The version of the CloudEvents specification which the event uses.
    /// REQUIRED.
    /// </summary>
    [JsonPropertyName("specversion")]
    public string SpecVersion { get; set; } = "1.0";

    /// <summary>
    /// Describes the type of event related to the originating occurrence.
    /// REQUIRED.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Content type of the data value. Must adhere to RFC 2046.
    /// OPTIONAL.
    /// </summary>
    [JsonPropertyName("datacontenttype")]
    public string? DataContentType { get; set; } = "application/json";

    /// <summary>
    /// Identifies the schema that data adheres to.
    /// OPTIONAL.
    /// </summary>
    [JsonPropertyName("dataschema")]
    public string? DataSchema { get; set; }

    /// <summary>
    /// Describes the subject of the event in the context of the event producer.
    /// OPTIONAL.
    /// </summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>
    /// Timestamp of when the occurrence happened. Must adhere to RFC 3339.
    /// OPTIONAL.
    /// </summary>
    [JsonPropertyName("time")]
    public DateTimeOffset? Time { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Event payload. The event payload is encoded into a media format specified by datacontenttype.
    /// OPTIONAL.
    /// </summary>
    [JsonPropertyName("data")]
    public TData? Data { get; set; }

    /// <summary>
    /// Additional extension attributes for custom metadata.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}

/// <summary>
/// Non-generic CloudEvent for deserialization scenarios.
/// </summary>
public class CloudEvent : CloudEvent<object>
{
}
