namespace AIKernel.Core.Catalog.Caching;

public interface ICacheMetricsCollector
{
    void RecordCacheHit(string operation, string? resourceType = null);
    void RecordCacheMiss(string operation, string? resourceType = null);
    void RecordCacheWrite(string operation, long sizeBytes);
    void RecordCacheEviction(string operation);
    void RecordCacheError(string operation, Exception exception);
    void RecordCacheLatency(string operation, TimeSpan latency);
    Task PublishMetricsAsync(CancellationToken cancellationToken = default);
    CacheMetricsSnapshot GetSnapshot();
}

public class CacheMetricsSnapshot
{
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long TotalWrites { get; set; }
    public long TotalEvictions { get; set; }
    public long TotalErrors { get; set; }
    public double HitRate { get; set; }
    public long TotalBytesWritten { get; set; }
    public Dictionary<string, long> HitsByOperation { get; set; } = new();
    public Dictionary<string, long> MissesByOperation { get; set; } = new();
    public Dictionary<string, TimeSpan> AverageLatencyByOperation { get; set; } = new();
    public DateTime SnapshotTime { get; set; } = DateTime.UtcNow;
}
