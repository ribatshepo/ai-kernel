using System.Text.Json;
using AIKernel.Core.EventBus.Configuration;
using AIKernel.Core.EventBus.Metrics;
using AIKernel.Core.EventBus.Models;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIKernel.Core.EventBus.Consumers;

/// <summary>
/// Handles failed events and sends them to the dead letter queue with exponential backoff retry.
/// </summary>
public class DeadLetterQueueHandler : IDeadLetterQueueHandler, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<DeadLetterQueueHandler> _logger;
    private readonly IEventBusMetrics _metrics;
    private readonly DeadLetterQueueConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;

    public DeadLetterQueueHandler(
        IOptions<EventBusConfiguration> configuration,
        ILogger<DeadLetterQueueHandler> logger,
        IEventBusMetrics metrics)
    {
        var eventBusConfig = configuration.Value;
        _configuration = eventBusConfig.DeadLetterQueue;
        _logger = logger;
        _metrics = metrics;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var config = new ProducerConfig
        {
            BootstrapServers = eventBusConfig.Kafka.BootstrapServers,
            ClientId = $"{eventBusConfig.Producer.ClientId}-dlq",
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = eventBusConfig.Producer.MessageTimeoutMs,
            RequestTimeoutMs = eventBusConfig.Kafka.RequestTimeoutMs,
            SecurityProtocol = ParseSecurityProtocol(eventBusConfig.Kafka.SecurityProtocol),
            SaslMechanism = ParseSaslMechanism(eventBusConfig.Kafka.SaslMechanism),
            SaslUsername = eventBusConfig.Kafka.SaslUsername,
            SaslPassword = eventBusConfig.Kafka.SaslPassword,
            EnableSslCertificateVerification = eventBusConfig.Kafka.EnableSslCertificateVerification,
            SslCaLocation = eventBusConfig.Kafka.SslCaLocation
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("DLQ producer error: {Reason}", error.Reason);
                _metrics.RecordError("dlq_producer", error.Reason);
            })
            .Build();

        _logger.LogInformation(
            "Dead letter queue handler initialized. MaxRetries: {MaxRetries}, InitialRetryDelay: {InitialRetryDelay}ms",
            _configuration.MaxRetries,
            _configuration.InitialRetryDelayMs);
    }

    public async Task HandleFailedEventAsync(
        DeadLetterEvent deadLetterEvent,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.EnableDlq)
        {
            _logger.LogWarning(
                "Dead letter queue is disabled. Event will be dropped. EventId: {EventId}, Topic: {Topic}",
                deadLetterEvent.EventId,
                deadLetterEvent.Topic);
            return;
        }

        try
        {
            // Check if we should retry or send to DLQ
            if (deadLetterEvent.AttemptCount < _configuration.MaxRetries)
            {
                var retrySuccessful = await RetryEventAsync(deadLetterEvent, cancellationToken);

                if (retrySuccessful)
                {
                    _logger.LogInformation(
                        "Event retry successful. EventId: {EventId}, Topic: {Topic}, Attempt: {Attempt}",
                        deadLetterEvent.EventId,
                        deadLetterEvent.Topic,
                        deadLetterEvent.AttemptCount);
                    return;
                }
            }

            // Send to DLQ after max retries exceeded or retry failed
            await SendToDeadLetterQueueAsync(deadLetterEvent, cancellationToken);

            _logger.LogWarning(
                "Event sent to dead letter queue. EventId: {EventId}, Topic: {Topic}, Attempts: {Attempts}",
                deadLetterEvent.EventId,
                deadLetterEvent.Topic,
                deadLetterEvent.AttemptCount);

            _metrics.RecordDeadLetter(deadLetterEvent.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle dead letter event. EventId: {EventId}, Topic: {Topic}",
                deadLetterEvent.EventId,
                deadLetterEvent.Topic);

            _metrics.RecordError("dlq_handler", ex.Message);
        }
    }

    public async Task<bool> RetryEventAsync(
        DeadLetterEvent deadLetterEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Calculate exponential backoff delay
            var delayMs = CalculateRetryDelay(deadLetterEvent.AttemptCount);

            _logger.LogInformation(
                "Retrying event after {Delay}ms. EventId: {EventId}, Topic: {Topic}, Attempt: {Attempt}/{MaxRetries}",
                delayMs,
                deadLetterEvent.EventId,
                deadLetterEvent.Topic,
                deadLetterEvent.AttemptCount + 1,
                _configuration.MaxRetries);

            await Task.Delay(delayMs, cancellationToken);

            // Increment attempt count
            deadLetterEvent.AttemptCount++;
            deadLetterEvent.LastFailureAt = DateTimeOffset.UtcNow;

            // In a real implementation, you would re-submit the event to the original topic
            // For now, we'll just return false to indicate retry failed
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during event retry. EventId: {EventId}, Topic: {Topic}",
                deadLetterEvent.EventId,
                deadLetterEvent.Topic);

            return false;
        }
    }

    private async Task SendToDeadLetterQueueAsync(
        DeadLetterEvent deadLetterEvent,
        CancellationToken cancellationToken)
    {
        var dlqTopic = deadLetterEvent.Topic + _configuration.TopicSuffix;
        var key = deadLetterEvent.EventId;
        var value = JsonSerializer.Serialize(deadLetterEvent, _jsonOptions);

        var message = new Message<string, string>
        {
            Key = key,
            Value = value,
            Headers = new Headers
            {
                { "original-topic", System.Text.Encoding.UTF8.GetBytes(deadLetterEvent.Topic) },
                { "error-message", System.Text.Encoding.UTF8.GetBytes(deadLetterEvent.ErrorMessage) },
                { "attempt-count", System.Text.Encoding.UTF8.GetBytes(deadLetterEvent.AttemptCount.ToString()) },
                { "consumer-group", System.Text.Encoding.UTF8.GetBytes(deadLetterEvent.ConsumerGroup) }
            },
            Timestamp = new Timestamp(DateTimeOffset.UtcNow)
        };

        try
        {
            var deliveryResult = await _producer.ProduceAsync(dlqTopic, message, cancellationToken);

            _logger.LogInformation(
                "Dead letter event sent. DLQ Topic: {DlqTopic}, Partition: {Partition}, Offset: {Offset}",
                deliveryResult.Topic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex,
                "Failed to send dead letter event to DLQ. Topic: {DlqTopic}, Error: {ErrorCode} - {ErrorReason}",
                dlqTopic,
                ex.Error.Code,
                ex.Error.Reason);

            throw;
        }
    }

    private int CalculateRetryDelay(int attemptCount)
    {
        // Exponential backoff: initialDelay * (multiplier ^ attemptCount)
        var delay = _configuration.InitialRetryDelayMs *
                   Math.Pow(_configuration.RetryBackoffMultiplier, attemptCount);

        // Cap at max retry delay
        return Math.Min((int)delay, _configuration.MaxRetryDelayMs);
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
        _logger.LogInformation("Dead letter queue handler disposed");
    }
}
