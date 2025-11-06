using System.Text;
using Microsoft.AspNetCore.Http;

namespace AIKernel.Core.Catalog.Caching;

public class PrometheusMetricsExporter
{
    private readonly ICacheMetricsCollector _metricsCollector;

    public PrometheusMetricsExporter(ICacheMetricsCollector metricsCollector)
    {
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
    }

    public async Task ExportMetricsAsync(HttpContext context)
    {
        var snapshot = _metricsCollector.GetSnapshot();
        var metrics = new StringBuilder();

        metrics.AppendLine("# HELP aikernel_cache_hits_total Total number of cache hits");
        metrics.AppendLine("# TYPE aikernel_cache_hits_total counter");
        metrics.AppendLine($"aikernel_cache_hits_total {snapshot.TotalHits}");
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_misses_total Total number of cache misses");
        metrics.AppendLine("# TYPE aikernel_cache_misses_total counter");
        metrics.AppendLine($"aikernel_cache_misses_total {snapshot.TotalMisses}");
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_writes_total Total number of cache writes");
        metrics.AppendLine("# TYPE aikernel_cache_writes_total counter");
        metrics.AppendLine($"aikernel_cache_writes_total {snapshot.TotalWrites}");
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_evictions_total Total number of cache evictions");
        metrics.AppendLine("# TYPE aikernel_cache_evictions_total counter");
        metrics.AppendLine($"aikernel_cache_evictions_total {snapshot.TotalEvictions}");
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_errors_total Total number of cache errors");
        metrics.AppendLine("# TYPE aikernel_cache_errors_total counter");
        metrics.AppendLine($"aikernel_cache_errors_total {snapshot.TotalErrors}");
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_hit_rate Cache hit rate (0.0 to 1.0)");
        metrics.AppendLine("# TYPE aikernel_cache_hit_rate gauge");
        metrics.AppendLine($"aikernel_cache_hit_rate {snapshot.HitRate:F6}");
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_bytes_written_total Total bytes written to cache");
        metrics.AppendLine("# TYPE aikernel_cache_bytes_written_total counter");
        metrics.AppendLine($"aikernel_cache_bytes_written_total {snapshot.TotalBytesWritten}");
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_hits_by_operation Cache hits by operation");
        metrics.AppendLine("# TYPE aikernel_cache_hits_by_operation counter");
        foreach (var kvp in snapshot.HitsByOperation)
        {
            metrics.AppendLine($"aikernel_cache_hits_by_operation{{operation=\"{EscapeLabel(kvp.Key)}\"}} {kvp.Value}");
        }
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_misses_by_operation Cache misses by operation");
        metrics.AppendLine("# TYPE aikernel_cache_misses_by_operation counter");
        foreach (var kvp in snapshot.MissesByOperation)
        {
            metrics.AppendLine($"aikernel_cache_misses_by_operation{{operation=\"{EscapeLabel(kvp.Key)}\"}} {kvp.Value}");
        }
        metrics.AppendLine();

        metrics.AppendLine("# HELP aikernel_cache_latency_seconds Average cache operation latency in seconds");
        metrics.AppendLine("# TYPE aikernel_cache_latency_seconds gauge");
        foreach (var kvp in snapshot.AverageLatencyByOperation)
        {
            metrics.AppendLine($"aikernel_cache_latency_seconds{{operation=\"{EscapeLabel(kvp.Key)}\"}} {kvp.Value.TotalSeconds:F6}");
        }

        context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
        await context.Response.WriteAsync(metrics.ToString());
    }

    private static string EscapeLabel(string label)
    {
        return label.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
