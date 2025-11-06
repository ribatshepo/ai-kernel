using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AIKernel.Core.EventBus.Metrics;

/// <summary>
/// Collects and aggregates event bus metrics.
/// </summary>
public class EventBusMetricsCollector : IEventBusMetrics
{
    private readonly ILogger<EventBusMetricsCollector> _logger;
    private readonly ConcurrentDictionary<string, TopicMetrics> _topicMetrics = new();
    private readonly ConcurrentDictionary<string, long> _errorCounts = new();
    private long _totalPublishes;
    private long _totalConsumes;
    private long _totalDeadLetters;
    private long _totalErrors;
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    public EventBusMetricsCollector(ILogger<EventBusMetricsCollector> logger)
    {
        _logger = logger;
    }

    public void RecordPublish(string topic, double latencyMs, int messageSizeBytes)
    {
        Interlocked.Increment(ref _totalPublishes);

        var metrics = _topicMetrics.GetOrAdd(topic, _ => new TopicMetrics());
        metrics.RecordPublish(latencyMs, messageSizeBytes);

        _logger.LogTrace(
            "Publish recorded. Topic: {Topic}, Latency: {Latency}ms, Size: {Size}B",
            topic,
            latencyMs,
            messageSizeBytes);
    }

    public void RecordConsume(string topic, double latencyMs, int messageSizeBytes)
    {
        Interlocked.Increment(ref _totalConsumes);

        var metrics = _topicMetrics.GetOrAdd(topic, _ => new TopicMetrics());
        metrics.RecordConsume(latencyMs, messageSizeBytes);

        _logger.LogTrace(
            "Consume recorded. Topic: {Topic}, Latency: {Latency}ms, Size: {Size}B",
            topic,
            latencyMs,
            messageSizeBytes);
    }

    public void RecordError(string component, string errorMessage)
    {
        Interlocked.Increment(ref _totalErrors);
        _errorCounts.AddOrUpdate(component, 1, (_, count) => count + 1);

        _logger.LogDebug(
            "Error recorded. Component: {Component}, Error: {Error}",
            component,
            errorMessage);
    }

    public void RecordDeadLetter(string topic)
    {
        Interlocked.Increment(ref _totalDeadLetters);

        var metrics = _topicMetrics.GetOrAdd(topic, _ => new TopicMetrics());
        metrics.RecordDeadLetter();

        _logger.LogDebug("Dead letter recorded. Topic: {Topic}", topic);
    }

    public Dictionary<string, object> GetSnapshot()
    {
        var uptime = DateTimeOffset.UtcNow - _startTime;

        var snapshot = new Dictionary<string, object>
        {
            ["uptime_seconds"] = uptime.TotalSeconds,
            ["total_publishes"] = _totalPublishes,
            ["total_consumes"] = _totalConsumes,
            ["total_dead_letters"] = _totalDeadLetters,
            ["total_errors"] = _totalErrors,
            ["publish_rate_per_second"] = _totalPublishes / uptime.TotalSeconds,
            ["consume_rate_per_second"] = _totalConsumes / uptime.TotalSeconds,
            ["error_rate_per_second"] = _totalErrors / uptime.TotalSeconds
        };

        // Add per-topic metrics
        foreach (var kvp in _topicMetrics)
        {
            var topic = kvp.Key;
            var metrics = kvp.Value;
            var topicSnapshot = metrics.GetSnapshot();

            foreach (var metric in topicSnapshot)
            {
                snapshot[$"topic.{topic}.{metric.Key}"] = metric.Value;
            }
        }

        // Add error counts by component
        foreach (var kvp in _errorCounts)
        {
            snapshot[$"errors.{kvp.Key}"] = kvp.Value;
        }

        return snapshot;
    }

    private class TopicMetrics
    {
        private long _publishCount;
        private long _consumeCount;
        private long _deadLetterCount;
        private long _totalPublishLatencyMs;
        private long _totalConsumeLatencyMs;
        private long _totalPublishBytes;
        private long _totalConsumeBytes;

        public void RecordPublish(double latencyMs, int messageSizeBytes)
        {
            Interlocked.Increment(ref _publishCount);
            Interlocked.Add(ref _totalPublishLatencyMs, (long)latencyMs);
            Interlocked.Add(ref _totalPublishBytes, messageSizeBytes);
        }

        public void RecordConsume(double latencyMs, int messageSizeBytes)
        {
            Interlocked.Increment(ref _consumeCount);
            Interlocked.Add(ref _totalConsumeLatencyMs, (long)latencyMs);
            Interlocked.Add(ref _totalConsumeBytes, messageSizeBytes);
        }

        public void RecordDeadLetter()
        {
            Interlocked.Increment(ref _deadLetterCount);
        }

        public Dictionary<string, object> GetSnapshot()
        {
            var snapshot = new Dictionary<string, object>
            {
                ["publish_count"] = _publishCount,
                ["consume_count"] = _consumeCount,
                ["dead_letter_count"] = _deadLetterCount,
                ["total_publish_bytes"] = _totalPublishBytes,
                ["total_consume_bytes"] = _totalConsumeBytes
            };

            if (_publishCount > 0)
            {
                snapshot["avg_publish_latency_ms"] = (double)_totalPublishLatencyMs / _publishCount;
            }

            if (_consumeCount > 0)
            {
                snapshot["avg_consume_latency_ms"] = (double)_totalConsumeLatencyMs / _consumeCount;
            }

            return snapshot;
        }
    }
}
