using AIKernel.Core.EventBus.Configuration;
using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIKernel.Core.EventBus.Health;

/// <summary>
/// Health check for Kafka connectivity.
/// </summary>
public class KafkaHealthCheck : IHealthCheck
{
    private readonly EventBusConfiguration _configuration;
    private readonly ILogger<KafkaHealthCheck> _logger;

    public KafkaHealthCheck(
        IOptions<EventBusConfiguration> configuration,
        ILogger<KafkaHealthCheck> logger)
    {
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = new AdminClientConfig
            {
                BootstrapServers = _configuration.Kafka.BootstrapServers,
                SocketTimeoutMs = 5000,
                SecurityProtocol = ParseSecurityProtocol(_configuration.Kafka.SecurityProtocol),
                SaslMechanism = ParseSaslMechanism(_configuration.Kafka.SaslMechanism),
                SaslUsername = _configuration.Kafka.SaslUsername,
                SaslPassword = _configuration.Kafka.SaslPassword,
                EnableSslCertificateVerification = _configuration.Kafka.EnableSslCertificateVerification,
                SslCaLocation = _configuration.Kafka.SslCaLocation
            };

            using var adminClient = new AdminClientBuilder(config).Build();

            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));

            if (metadata == null || metadata.Brokers == null || !metadata.Brokers.Any())
            {
                _logger.LogWarning("Kafka health check failed: No brokers available");
                return HealthCheckResult.Unhealthy("No Kafka brokers available");
            }

            var data = new Dictionary<string, object>
            {
                ["broker_count"] = metadata.Brokers.Count,
                ["topic_count"] = metadata.Topics?.Count ?? 0,
                ["brokers"] = string.Join(", ", metadata.Brokers.Select(b => $"{b.Host}:{b.Port}"))
            };

            _logger.LogDebug("Kafka health check passed. Brokers: {BrokerCount}", metadata.Brokers.Count);

            return HealthCheckResult.Healthy(
                $"Kafka is healthy. {metadata.Brokers.Count} broker(s) available.",
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka health check failed");

            return HealthCheckResult.Unhealthy(
                "Kafka is unhealthy",
                ex,
                new Dictionary<string, object>
                {
                    ["bootstrap_servers"] = _configuration.Kafka.BootstrapServers,
                    ["error"] = ex.Message
                });
        }
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
}
