namespace AIKernel.Core.Catalog.Caching;

public class CachingConfiguration
{
    public const string SectionName = "Caching";

    public RedisConfiguration Redis { get; set; } = new();
    public CacheDefaultsConfiguration Defaults { get; set; } = new();
    public MetricsConfiguration Metrics { get; set; } = new();
}

public class RedisConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "aikernel_";
    public int ConnectTimeout { get; set; } = 5000;
    public int SyncTimeout { get; set; } = 5000;
    public int ConnectRetry { get; set; } = 3;
    public bool AbortOnConnectFail { get; set; } = false;
    public string? SentinelConfiguration { get; set; }
    public bool UseSsl { get; set; } = false;
    public string? SslHost { get; set; }
}

public class CacheDefaultsConfiguration
{
    public int ResourceCacheTtlSeconds { get; set; } = 300;
    public int SearchResultsTtlSeconds { get; set; } = 600;
    public int VersionHistoryTtlSeconds { get; set; } = 3600;
    public int LineageQueryTtlSeconds { get; set; } = 900;
    public int RelationshipCacheTtlSeconds { get; set; } = 600;
    public bool EnableCompression { get; set; } = true;
    public int CompressionThresholdBytes { get; set; } = 1024;
}

public class MetricsConfiguration
{
    public bool Enabled { get; set; } = true;
    public int PublishIntervalSeconds { get; set; } = 60;
    public bool EnableDetailedMetrics { get; set; } = true;
}
