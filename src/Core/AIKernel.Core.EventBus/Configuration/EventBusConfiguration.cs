namespace AIKernel.Core.EventBus.Configuration;

public class EventBusConfiguration
{
    public const string SectionName = "EventBus";

    public KafkaConfiguration Kafka { get; set; } = new();
    public SchemaRegistryConfiguration SchemaRegistry { get; set; } = new();
    public ProducerConfiguration Producer { get; set; } = new();
    public ConsumerConfiguration Consumer { get; set; } = new();
    public DeadLetterQueueConfiguration DeadLetterQueue { get; set; } = new();
    public MetricsConfiguration Metrics { get; set; } = new();
}

public class KafkaConfiguration
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string? SecurityProtocol { get; set; }
    public string? SaslMechanism { get; set; }
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
    public bool EnableSslCertificateVerification { get; set; } = true;
    public string? SslCaLocation { get; set; }
    public int ConnectionsMaxIdleMs { get; set; } = 540000;
    public int MetadataMaxAgeMs { get; set; } = 900000;
    public int SocketTimeoutMs { get; set; } = 60000;
    public int RequestTimeoutMs { get; set; } = 30000;
}

public class SchemaRegistryConfiguration
{
    public string Url { get; set; } = "http://localhost:8081";
    public string? BasicAuthUserInfo { get; set; }
    public int RequestTimeoutMs { get; set; } = 30000;
    public int MaxCachedSchemas { get; set; } = 1000;
    public bool AutoRegisterSchemas { get; set; } = true;
}

public class ProducerConfiguration
{
    public string ClientId { get; set; } = "aikernel-producer";
    public string Acks { get; set; } = "all";
    public int Retries { get; set; } = 3;
    public int RetryBackoffMs { get; set; } = 100;
    public int LingerMs { get; set; } = 10;
    public int BatchSize { get; set; } = 16384;
    public string CompressionType { get; set; } = "snappy";
    public int MaxInFlight { get; set; } = 5;
    public bool EnableIdempotence { get; set; } = true;
    public int MessageTimeoutMs { get; set; } = 300000;
    public int MessageMaxBytes { get; set; } = 1048576;
}

public class ConsumerConfiguration
{
    public string ClientId { get; set; } = "aikernel-consumer";
    public string GroupId { get; set; } = "aikernel-consumer-group";
    public string AutoOffsetReset { get; set; } = "earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public int AutoCommitIntervalMs { get; set; } = 5000;
    public int SessionTimeoutMs { get; set; } = 45000;
    public int HeartbeatIntervalMs { get; set; } = 3000;
    public int MaxPollIntervalMs { get; set; } = 300000;
    public int MaxPollRecords { get; set; } = 500;
    public int FetchMinBytes { get; set; } = 1;
    public int FetchMaxWaitMs { get; set; } = 500;
    public int MaxPartitionFetchBytes { get; set; } = 1048576;
}

public class DeadLetterQueueConfiguration
{
    public string TopicSuffix { get; set; } = ".dlq";
    public int MaxRetries { get; set; } = 5;
    public int InitialRetryDelayMs { get; set; } = 1000;
    public double RetryBackoffMultiplier { get; set; } = 2.0;
    public int MaxRetryDelayMs { get; set; } = 60000;
    public bool EnableDlq { get; set; } = true;
}

public class MetricsConfiguration
{
    public bool Enabled { get; set; } = true;
    public int PublishIntervalSeconds { get; set; } = 60;
    public bool EnableDetailedMetrics { get; set; } = true;
    public string MetricsPrefix { get; set; } = "aikernel_eventbus";
}
