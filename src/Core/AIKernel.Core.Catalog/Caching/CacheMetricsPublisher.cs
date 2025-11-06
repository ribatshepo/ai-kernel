using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIKernel.Core.Catalog.Caching;

public class CacheMetricsPublisher : BackgroundService
{
    private readonly ICacheMetricsCollector _metricsCollector;
    private readonly MetricsConfiguration _config;
    private readonly ILogger<CacheMetricsPublisher> _logger;

    public CacheMetricsPublisher(
        ICacheMetricsCollector metricsCollector,
        MetricsConfiguration config,
        ILogger<CacheMetricsPublisher> logger)
    {
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Cache metrics publishing is disabled");
            return;
        }

        _logger.LogInformation(
            "Cache metrics publisher starting. Publish interval: {Interval} seconds",
            _config.PublishIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_config.PublishIntervalSeconds),
                    stoppingToken);

                await _metricsCollector.PublishMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cache metrics publisher is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing cache metrics");
            }
        }

        _logger.LogInformation("Cache metrics publisher stopped");
    }
}
