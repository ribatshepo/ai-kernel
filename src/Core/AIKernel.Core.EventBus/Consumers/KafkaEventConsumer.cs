using System.Text.Json;
using AIKernel.Core.EventBus.Configuration;
using AIKernel.Core.EventBus.Metrics;
using AIKernel.Core.EventBus.Models;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIKernel.Core.EventBus.Consumers;

/// <summary>
/// Kafka implementation of event consumer with at-least-once delivery semantics.
/// </summary>
public class KafkaEventConsumer : IEventConsumer, IDisposable
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaEventConsumer> _logger;
    private readonly IEventBusMetrics _metrics;
    private readonly IDeadLetterQueueHandler _dlqHandler;
    private readonly IEventHandlerRegistry _handlerRegistry;
    private readonly EventBusConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _consumeTask;

    public KafkaEventConsumer(
        IOptions<EventBusConfiguration> configuration,
        IServiceProvider serviceProvider,
        ILogger<KafkaEventConsumer> logger,
        IEventBusMetrics metrics,
        IDeadLetterQueueHandler dlqHandler,
        IEventHandlerRegistry handlerRegistry)
    {
        _configuration = configuration.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _metrics = metrics;
        _dlqHandler = dlqHandler;
        _handlerRegistry = handlerRegistry;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration.Kafka.BootstrapServers,
            GroupId = _configuration.Consumer.GroupId,
            ClientId = _configuration.Consumer.ClientId,
            AutoOffsetReset = ParseAutoOffsetReset(_configuration.Consumer.AutoOffsetReset),
            EnableAutoCommit = _configuration.Consumer.EnableAutoCommit,
            AutoCommitIntervalMs = _configuration.Consumer.AutoCommitIntervalMs,
            SessionTimeoutMs = _configuration.Consumer.SessionTimeoutMs,
            HeartbeatIntervalMs = _configuration.Consumer.HeartbeatIntervalMs,
            MaxPollIntervalMs = _configuration.Consumer.MaxPollIntervalMs,
            FetchMinBytes = _configuration.Consumer.FetchMinBytes,
            FetchWaitMaxMs = _configuration.Consumer.FetchMaxWaitMs,
            MaxPartitionFetchBytes = _configuration.Consumer.MaxPartitionFetchBytes,
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

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka consumer error: {Reason}", error.Reason);
                _metrics.RecordError("consumer", error.Reason);
            })
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation(
                    "Partitions assigned: {Partitions}",
                    string.Join(", ", partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                _logger.LogInformation(
                    "Partitions revoked: {Partitions}",
                    string.Join(", ", partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .Build();

        _logger.LogInformation(
            "Kafka event consumer initialized. ClientId: {ClientId}, GroupId: {GroupId}, Brokers: {Brokers}",
            _configuration.Consumer.ClientId,
            _configuration.Consumer.GroupId,
            _configuration.Kafka.BootstrapServers);
    }

    public Task StartAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default)
    {
        var topicList = topics.ToList();

        if (!topicList.Any())
        {
            throw new ArgumentException("At least one topic must be specified", nameof(topics));
        }

        _logger.LogInformation("Starting event consumer for topics: {Topics}", string.Join(", ", topicList));

        _consumer.Subscribe(topicList);
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumeTask = Task.Run(() => ConsumeLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping event consumer");

        _cancellationTokenSource?.Cancel();

        if (_consumeTask != null)
        {
            try
            {
                await _consumeTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        _consumer.Close();
        _logger.LogInformation("Event consumer stopped");
    }

    private async Task ConsumeLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(cancellationToken);

                if (consumeResult?.Message == null)
                    continue;

                await ProcessMessageAsync(consumeResult, cancellationToken);

                // Manual commit for at-least-once delivery
                if (!_configuration.Consumer.EnableAutoCommit)
                {
                    _consumer.Commit(consumeResult);
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming message: {Reason}", ex.Error.Reason);
                _metrics.RecordError("consumer", ex.Error.Reason);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in consumer loop");
                _metrics.RecordError("consumer", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        var topic = consumeResult.Topic;
        var partition = consumeResult.Partition.Value;
        var offset = consumeResult.Offset.Value;

        try
        {
            _logger.LogDebug(
                "Processing message. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                topic,
                partition,
                offset);

            // Deserialize the envelope (type information will be in the CloudEvent.Type field)
            var envelope = JsonSerializer.Deserialize<EventEnvelope<object>>(
                consumeResult.Message.Value,
                _jsonOptions);

            if (envelope == null)
            {
                throw new InvalidOperationException("Failed to deserialize event envelope");
            }

            // Extract metadata from Kafka headers
            ExtractMetadataFromHeaders(consumeResult.Message.Headers, envelope.Metadata);

            // Dispatch to the appropriate handler
            await DispatchToHandlerAsync(envelope, cancellationToken);

            var latencyMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _metrics.RecordConsume(topic, latencyMs, consumeResult.Message.Value.Length);

            _logger.LogDebug(
                "Message processed successfully. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, Latency: {Latency}ms",
                topic,
                partition,
                offset,
                latencyMs);
        }
        catch (Exception ex)
        {
            var latencyMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _metrics.RecordError(topic, ex.Message);

            _logger.LogError(ex,
                "Failed to process message. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                topic,
                partition,
                offset);

            // Send to dead letter queue
            await _dlqHandler.HandleFailedEventAsync(
                new DeadLetterEvent
                {
                    Topic = topic,
                    Partition = partition,
                    Offset = offset,
                    Payload = consumeResult.Message.Value,
                    ErrorMessage = ex.Message,
                    ExceptionDetails = ex.ToString(),
                    ConsumerGroup = _configuration.Consumer.GroupId,
                    AttemptCount = 1
                },
                cancellationToken);

            // Re-throw to prevent commit (at-least-once delivery)
            throw;
        }
    }

    private async Task DispatchToHandlerAsync(
        EventEnvelope<object> envelope,
        CancellationToken cancellationToken)
    {
        var eventTypeName = envelope.Event.Type;

        if (!_handlerRegistry.IsRegistered(eventTypeName))
        {
            _logger.LogWarning(
                "No handler registered for event type {EventType}. Event will be skipped. EventId: {EventId}",
                eventTypeName,
                envelope.Event.Id);
            return;
        }

        var eventDataType = _handlerRegistry.GetEventDataType(eventTypeName);
        var handlerType = _handlerRegistry.GetHandlerType(eventTypeName);

        if (eventDataType == null || handlerType == null)
        {
            _logger.LogError(
                "Handler registration incomplete for event type {EventType}. EventId: {EventId}",
                eventTypeName,
                envelope.Event.Id);
            return;
        }

        try
        {
            // Create a scope for each message to get scoped services
            using var scope = _serviceProvider.CreateScope();

            // Deserialize the event data to the correct type
            var typedEnvelope = DeserializeToTypedEnvelope(envelope, eventDataType);

            // Resolve the handler from the service provider
            var handler = scope.ServiceProvider.GetService(handlerType);

            if (handler == null)
            {
                _logger.LogError(
                    "Failed to resolve handler {HandlerType} for event type {EventType}. Ensure the handler is registered in DI. EventId: {EventId}",
                    handlerType.Name,
                    eventTypeName,
                    envelope.Event.Id);
                return;
            }

            // Invoke the handler's HandleAsync method using reflection
            var handleMethod = handlerType.GetMethod("HandleAsync");
            if (handleMethod == null)
            {
                _logger.LogError(
                    "Handler {HandlerType} does not have a HandleAsync method. EventId: {EventId}",
                    handlerType.Name,
                    envelope.Event.Id);
                return;
            }

            _logger.LogDebug(
                "Dispatching event to handler. EventType: {EventType}, Handler: {Handler}, EventId: {EventId}, CorrelationId: {CorrelationId}",
                eventTypeName,
                handlerType.Name,
                envelope.Event.Id,
                envelope.Metadata.CorrelationId);

            var handleTask = handleMethod.Invoke(handler, new[] { typedEnvelope, cancellationToken });
            if (handleTask is Task task)
            {
                await task;
            }

            _logger.LogDebug(
                "Event handled successfully. EventType: {EventType}, Handler: {Handler}, EventId: {EventId}",
                eventTypeName,
                handlerType.Name,
                envelope.Event.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error dispatching event to handler. EventType: {EventType}, EventId: {EventId}",
                eventTypeName,
                envelope.Event.Id);
            throw;
        }
    }

    private object DeserializeToTypedEnvelope(EventEnvelope<object> envelope, Type eventDataType)
    {
        // Serialize the envelope back to JSON
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);

        // Deserialize to the typed envelope using the correct event data type
        var envelopeType = typeof(EventEnvelope<>).MakeGenericType(eventDataType);
        var typedEnvelope = JsonSerializer.Deserialize(json, envelopeType, _jsonOptions);

        if (typedEnvelope == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize envelope to type {envelopeType.Name}");
        }

        return typedEnvelope;
    }

    private void ExtractMetadataFromHeaders(Headers headers, EventMetadata metadata)
    {
        foreach (var header in headers)
        {
            var value = System.Text.Encoding.UTF8.GetString(header.GetValueBytes());

            switch (header.Key.ToLowerInvariant())
            {
                case "correlation-id":
                    metadata.CorrelationId = value;
                    break;
                case "causation-id":
                    metadata.CausationId = value;
                    break;
                case "tenant-id":
                    metadata.TenantId = value;
                    break;
                case "user-id":
                    metadata.UserId = value;
                    break;
                case "priority":
                    if (int.TryParse(value, out var priority))
                        metadata.Priority = priority;
                    break;
                default:
                    metadata.Headers[header.Key] = value;
                    break;
            }
        }
    }

    private static AutoOffsetReset ParseAutoOffsetReset(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            "error" => AutoOffsetReset.Error,
            _ => AutoOffsetReset.Earliest
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
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _consumer?.Close();
        _consumer?.Dispose();
        _logger.LogInformation("Kafka event consumer disposed");
    }
}
