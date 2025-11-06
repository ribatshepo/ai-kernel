using System.Text.Json;
using AIKernel.Core.EventBus.Configuration;
using AIKernel.Core.EventBus.Metrics;
using AIKernel.Core.EventBus.Models;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIKernel.Core.EventBus.Producers;

/// <summary>
/// Kafka implementation of the event producer with exactly-once semantics.
/// </summary>
public class KafkaEventProducer : IEventProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventProducer> _logger;
    private readonly IEventBusMetrics _metrics;
    private readonly EventBusConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _sourceName;

    public KafkaEventProducer(
        IOptions<EventBusConfiguration> configuration,
        ILogger<KafkaEventProducer> logger,
        IEventBusMetrics metrics)
    {
        _configuration = configuration.Value;
        _logger = logger;
        _metrics = metrics;
        _sourceName = $"aikernel.{_configuration.Producer.ClientId}";

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var config = new ProducerConfig
        {
            BootstrapServers = _configuration.Kafka.BootstrapServers,
            ClientId = _configuration.Producer.ClientId,
            Acks = ParseAcks(_configuration.Producer.Acks),
            EnableIdempotence = _configuration.Producer.EnableIdempotence,
            MaxInFlight = _configuration.Producer.MaxInFlight,
            MessageTimeoutMs = _configuration.Producer.MessageTimeoutMs,
            RequestTimeoutMs = _configuration.Kafka.RequestTimeoutMs,
            MessageSendMaxRetries = _configuration.Producer.Retries,
            RetryBackoffMs = _configuration.Producer.RetryBackoffMs,
            LingerMs = _configuration.Producer.LingerMs,
            BatchSize = _configuration.Producer.BatchSize,
            CompressionType = ParseCompressionType(_configuration.Producer.CompressionType),
            MessageMaxBytes = _configuration.Producer.MessageMaxBytes,
            SecurityProtocol = ParseSecurityProtocol(_configuration.Kafka.SecurityProtocol),
            SaslMechanism = ParseSaslMechanism(_configuration.Kafka.SaslMechanism),
            SaslUsername = _configuration.Kafka.SaslUsername,
            SaslPassword = _configuration.Kafka.SaslPassword,
            EnableSslCertificateVerification = _configuration.Kafka.EnableSslCertificateVerification,
            SslCaLocation = _configuration.Kafka.SslCaLocation,
            ConnectionsMaxIdleMs = _configuration.Kafka.ConnectionsMaxIdleMs,
            MetadataMaxAgeMs = _configuration.Kafka.MetadataMaxAgeMs,
            SocketTimeoutMs = _configuration.Kafka.SocketTimeoutMs
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka producer error: {Reason}", error.Reason);
                _metrics.RecordError("producer", error.Reason);
            })
            .SetStatisticsHandler((_, statistics) =>
            {
                _logger.LogTrace("Kafka producer statistics: {Statistics}", statistics);
            })
            .Build();

        _logger.LogInformation(
            "Kafka event producer initialized. ClientId: {ClientId}, Brokers: {Brokers}, Idempotence: {Idempotence}",
            _configuration.Producer.ClientId,
            _configuration.Kafka.BootstrapServers,
            _configuration.Producer.EnableIdempotence);
    }

    public async Task<string> PublishAsync<TData>(
        string topic,
        TData data,
        string? partitionKey = null,
        CancellationToken cancellationToken = default) where TData : class
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var envelope = EventEnvelope<TData>.Create(
                data: data,
                eventType: typeof(TData).Name,
                source: _sourceName,
                subject: topic,
                partitionKey: partitionKey);

            var key = partitionKey ?? envelope.Event.Id;
            var value = JsonSerializer.Serialize(envelope, _jsonOptions);

            var message = new Message<string, string>
            {
                Key = key,
                Value = value,
                Headers = CreateHeaders(envelope),
                Timestamp = new Timestamp(envelope.Metadata.PublishedAt)
            };

            var deliveryResult = await _producer.ProduceAsync(topic, message, cancellationToken);

            var latencyMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _metrics.RecordPublish(topic, latencyMs, value.Length);

            _logger.LogDebug(
                "Event published successfully. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, EventId: {EventId}, Latency: {Latency}ms",
                deliveryResult.Topic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value,
                envelope.Event.Id,
                latencyMs);

            return envelope.Event.Id;
        }
        catch (ProduceException<string, string> ex)
        {
            var latencyMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _metrics.RecordError(topic, ex.Error.Reason);

            _logger.LogError(ex,
                "Failed to publish event to topic {Topic}. Error: {ErrorCode} - {ErrorReason}",
                topic,
                ex.Error.Code,
                ex.Error.Reason);

            throw new EventPublishException(
                $"Failed to publish event to topic '{topic}': {ex.Error.Reason}",
                ex);
        }
        catch (Exception ex)
        {
            var latencyMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _metrics.RecordError(topic, ex.Message);

            _logger.LogError(ex, "Unexpected error publishing event to topic {Topic}", topic);
            throw new EventPublishException($"Unexpected error publishing event to topic '{topic}'", ex);
        }
    }

    public async Task<IEnumerable<string>> PublishBatchAsync<TData>(
        string topic,
        IEnumerable<TData> events,
        CancellationToken cancellationToken = default) where TData : class
    {
        var eventIds = new List<string>();
        var tasks = new List<Task>();

        foreach (var eventData in events)
        {
            var task = PublishAsync(topic, eventData, cancellationToken: cancellationToken)
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        lock (eventIds)
                        {
                            eventIds.Add(t.Result);
                        }
                    }
                }, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        return eventIds;
    }

    public async Task FlushAsync(TimeSpan? timeout = null)
    {
        var timeoutMs = (int)(timeout ?? TimeSpan.FromSeconds(30)).TotalMilliseconds;

        try
        {
            _producer.Flush(TimeSpan.FromMilliseconds(timeoutMs));
            _logger.LogDebug("Producer flush completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing producer");
            throw;
        }

        await Task.CompletedTask;
    }

    private Headers CreateHeaders<TData>(EventEnvelope<TData> envelope) where TData : class
    {
        var headers = new Headers
        {
            { "correlation-id", System.Text.Encoding.UTF8.GetBytes(envelope.Metadata.CorrelationId) },
            { "schema-version", System.Text.Encoding.UTF8.GetBytes(envelope.SchemaVersion) }
        };

        if (envelope.Metadata.TenantId != null)
            headers.Add("tenant-id", System.Text.Encoding.UTF8.GetBytes(envelope.Metadata.TenantId));

        if (envelope.Metadata.UserId != null)
            headers.Add("user-id", System.Text.Encoding.UTF8.GetBytes(envelope.Metadata.UserId));

        if (envelope.Metadata.CausationId != null)
            headers.Add("causation-id", System.Text.Encoding.UTF8.GetBytes(envelope.Metadata.CausationId));

        headers.Add("priority", System.Text.Encoding.UTF8.GetBytes(envelope.Metadata.Priority.ToString()));

        foreach (var header in envelope.Metadata.Headers)
        {
            headers.Add(header.Key, System.Text.Encoding.UTF8.GetBytes(header.Value));
        }

        return headers;
    }

    private static Acks ParseAcks(string acksValue)
    {
        return acksValue.ToLowerInvariant() switch
        {
            "all" or "-1" => Acks.All,
            "1" => Acks.Leader,
            "0" => Acks.None,
            _ => Acks.All
        };
    }

    private static CompressionType ParseCompressionType(string compressionType)
    {
        return compressionType.ToLowerInvariant() switch
        {
            "gzip" => CompressionType.Gzip,
            "snappy" => CompressionType.Snappy,
            "lz4" => CompressionType.Lz4,
            "zstd" => CompressionType.Zstd,
            "none" => CompressionType.None,
            _ => CompressionType.Snappy
        };
    }

    private static SecurityProtocol? ParseSecurityProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
            return null;

        return protocol.ToLowerInvariant() switch
        {
            "plaintext" => SecurityProtocol.Plaintext,
            "ssl" => SecurityProtocol.Ssl,
            "sasl_plaintext" => SecurityProtocol.SaslPlaintext,
            "sasl_ssl" => SecurityProtocol.SaslSsl,
            _ => null
        };
    }

    private static SaslMechanism? ParseSaslMechanism(string? mechanism)
    {
        if (string.IsNullOrWhiteSpace(mechanism))
            return null;

        return mechanism.ToUpperInvariant() switch
        {
            "PLAIN" => SaslMechanism.Plain,
            "SCRAM-SHA-256" => SaslMechanism.ScramSha256,
            "SCRAM-SHA-512" => SaslMechanism.ScramSha512,
            "GSSAPI" => SaslMechanism.Gssapi,
            "OAUTHBEARER" => SaslMechanism.OAuthBearer,
            _ => null
        };
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
        _logger.LogInformation("Kafka event producer disposed");
    }
}

/// <summary>
/// Exception thrown when event publishing fails.
/// </summary>
public class EventPublishException : Exception
{
    public EventPublishException(string message) : base(message)
    {
    }

    public EventPublishException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
