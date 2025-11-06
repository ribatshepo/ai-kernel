using AIKernel.Core.Catalog.Contracts.Enums;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AIKernel.Core.Catalog.Caching;

public class CachedSearchRepository : ISearchRepository
{
    private readonly ISearchRepository _innerRepository;
    private readonly IDistributedCacheService _cacheService;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ICacheMetricsCollector _metricsCollector;
    private readonly ILogger<CachedSearchRepository> _logger;
    private readonly CacheDefaultsConfiguration _config;

    public CachedSearchRepository(
        ISearchRepository innerRepository,
        IDistributedCacheService cacheService,
        ICacheKeyGenerator keyGenerator,
        ICacheMetricsCollector metricsCollector,
        ILogger<CachedSearchRepository> logger,
        CacheDefaultsConfiguration config)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<IEnumerable<CatalogResource>> SearchAsync(
        string query,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateSearchKey(query, pageNumber, pageSize);

        var cachedResults = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedResults != null)
        {
            return cachedResults;
        }

        var results = await _innerRepository.SearchAsync(query, pageSize, pageNumber, cancellationToken);
        var resultsList = results.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            resultsList,
            TimeSpan.FromSeconds(_config.SearchResultsTtlSeconds),
            cancellationToken);

        return resultsList;
    }

    public async Task<IEnumerable<CatalogResource>> AutocompleteAsync(
        string prefix,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateAutocompleteKey(prefix, limit);

        var cachedResults = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedResults != null)
        {
            return cachedResults;
        }

        var results = await _innerRepository.AutocompleteAsync(prefix, limit, cancellationToken);
        var resultsList = results.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            resultsList,
            TimeSpan.FromSeconds(_config.SearchResultsTtlSeconds),
            cancellationToken);

        return resultsList;
    }

    public async Task<IEnumerable<CatalogResource>> SearchByTypeAsync(
        ResourceType resourceType,
        string? query = null,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateResourceByTypeKey(resourceType.ToString(), pageNumber, pageSize);

        var cachedResults = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedResults != null)
        {
            return cachedResults;
        }

        var results = await _innerRepository.SearchByTypeAsync(resourceType, query, pageSize, pageNumber, cancellationToken);
        var resultsList = results.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            resultsList,
            TimeSpan.FromSeconds(_config.SearchResultsTtlSeconds),
            cancellationToken);

        return resultsList;
    }

    public async Task<IEnumerable<CatalogResource>> SearchByNamespaceAsync(
        string @namespace,
        string? query = null,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateResourceByNamespaceKey(@namespace, pageNumber, pageSize);

        var cachedResults = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedResults != null)
        {
            return cachedResults;
        }

        var results = await _innerRepository.SearchByNamespaceAsync(@namespace, query, pageSize, pageNumber, cancellationToken);
        var resultsList = results.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            resultsList,
            TimeSpan.FromSeconds(_config.SearchResultsTtlSeconds),
            cancellationToken);

        return resultsList;
    }

    public async Task<IEnumerable<CatalogResource>> SearchByTagsAsync(
        IEnumerable<string> tags,
        bool matchAll = false,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var tagsList = tags.ToList();
        var cacheKey = _keyGenerator.GenerateResourceByTagsKey(tagsList, pageNumber, pageSize);

        var cachedResults = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedResults != null)
        {
            return cachedResults;
        }

        var results = await _innerRepository.SearchByTagsAsync(tagsList, matchAll, pageSize, pageNumber, cancellationToken);
        var resultsList = results.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            resultsList,
            TimeSpan.FromSeconds(_config.SearchResultsTtlSeconds),
            cancellationToken);

        return resultsList;
    }

    public async Task<Dictionary<string, long>> GetFacetsAsync(
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateFacetsKey(query ?? "all");

        var cachedFacets = await _cacheService.GetAsync<Dictionary<string, long>>(cacheKey, cancellationToken);

        if (cachedFacets != null)
        {
            return cachedFacets;
        }

        var facets = await _innerRepository.GetFacetsAsync(query, cancellationToken);

        await _cacheService.SetAsync(
            cacheKey,
            facets,
            TimeSpan.FromSeconds(_config.SearchResultsTtlSeconds),
            cancellationToken);

        return facets;
    }

    public async Task IndexAsync(CatalogResource resource, CancellationToken cancellationToken = default)
    {
        await _innerRepository.IndexAsync(resource, cancellationToken);
        await InvalidateSearchCachesAsync(resource, cancellationToken);
    }

    public async Task BulkIndexAsync(IEnumerable<CatalogResource> resources, CancellationToken cancellationToken = default)
    {
        await _innerRepository.BulkIndexAsync(resources, cancellationToken);

        foreach (var resource in resources)
        {
            await InvalidateSearchCachesAsync(resource, cancellationToken);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _innerRepository.DeleteAsync(id, cancellationToken);
        await InvalidateAllSearchCachesAsync(cancellationToken);
    }

    public async Task ReindexAllAsync(IEnumerable<CatalogResource> resources, CancellationToken cancellationToken = default)
    {
        await _innerRepository.ReindexAllAsync(resources, cancellationToken);
        await InvalidateAllSearchCachesAsync(cancellationToken);
    }

    private async Task InvalidateSearchCachesAsync(CatalogResource resource, CancellationToken cancellationToken)
    {
        try
        {
            var invalidationTasks = new List<Task>
            {
                _cacheService.RemoveByPatternAsync(
                    _keyGenerator.GenerateInvalidationPattern(CacheConstants.Prefixes.Search),
                    cancellationToken),
                _cacheService.RemoveByPatternAsync(
                    _keyGenerator.GenerateInvalidationPattern(CacheConstants.Prefixes.Facets),
                    cancellationToken)
            };

            await Task.WhenAll(invalidationTasks);

            _logger.LogDebug(
                "Invalidated search caches for resource {ResourceId}",
                resource.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to invalidate search caches for resource {ResourceId}",
                resource.Id);
        }
    }

    private async Task InvalidateAllSearchCachesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var invalidationTasks = new List<Task>
            {
                _cacheService.RemoveByPatternAsync(
                    _keyGenerator.GenerateInvalidationPattern(CacheConstants.Prefixes.Search),
                    cancellationToken),
                _cacheService.RemoveByPatternAsync(
                    _keyGenerator.GenerateInvalidationPattern(CacheConstants.Prefixes.Facets),
                    cancellationToken)
            };

            await Task.WhenAll(invalidationTasks);

            _logger.LogDebug("Invalidated all search caches");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate all search caches");
        }
    }
}
