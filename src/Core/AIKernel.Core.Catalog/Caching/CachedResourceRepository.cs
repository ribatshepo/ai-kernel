using AIKernel.Core.Catalog.Contracts.Enums;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AIKernel.Core.Catalog.Caching;

public class CachedResourceRepository : IResourceRepository
{
    private readonly IResourceRepository _innerRepository;
    private readonly IDistributedCacheService _cacheService;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ICacheMetricsCollector _metricsCollector;
    private readonly ILogger<CachedResourceRepository> _logger;
    private readonly CacheDefaultsConfiguration _config;

    public CachedResourceRepository(
        IResourceRepository innerRepository,
        IDistributedCacheService cacheService,
        ICacheKeyGenerator keyGenerator,
        ICacheMetricsCollector metricsCollector,
        ILogger<CachedResourceRepository> logger,
        CacheDefaultsConfiguration config)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<CatalogResource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateResourceKey(id);

        var cachedResource = await _cacheService.GetAsync<CatalogResource>(cacheKey, cancellationToken);

        if (cachedResource != null)
        {
            return cachedResource;
        }

        var resource = await _innerRepository.GetByIdAsync(id, cancellationToken);

        if (resource != null)
        {
            await _cacheService.SetAsync(
                cacheKey,
                resource,
                TimeSpan.FromSeconds(_config.ResourceCacheTtlSeconds),
                cancellationToken);
        }

        return resource;
    }

    public async Task<CatalogResource?> GetByNameAsync(
        string name,
        string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateResourceByNameKey(name);

        var cachedResource = await _cacheService.GetAsync<CatalogResource>(cacheKey, cancellationToken);

        if (cachedResource != null)
        {
            return cachedResource;
        }

        var resource = await _innerRepository.GetByNameAsync(name, @namespace, cancellationToken);

        if (resource != null)
        {
            await _cacheService.SetAsync(
                cacheKey,
                resource,
                TimeSpan.FromSeconds(_config.ResourceCacheTtlSeconds),
                cancellationToken);
        }

        return resource;
    }

    public async Task<IEnumerable<CatalogResource>> GetByTypeAsync(
        ResourceType resourceType,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateResourceByTypeKey(resourceType.ToString(), 1, 100);

        var cachedResources = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedResources != null)
        {
            return cachedResources;
        }

        var resources = await _innerRepository.GetByTypeAsync(resourceType, cancellationToken);
        var resourceList = resources.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            resourceList,
            TimeSpan.FromSeconds(_config.ResourceCacheTtlSeconds),
            cancellationToken);

        return resourceList;
    }

    public async Task<IEnumerable<CatalogResource>> GetByNamespaceAsync(
        string @namespace,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateResourceByNamespaceKey(@namespace, 1, 100);

        var cachedResources = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedResources != null)
        {
            return cachedResources;
        }

        var resources = await _innerRepository.GetByNamespaceAsync(@namespace, cancellationToken);
        var resourceList = resources.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            resourceList,
            TimeSpan.FromSeconds(_config.ResourceCacheTtlSeconds),
            cancellationToken);

        return resourceList;
    }

    public async Task<IEnumerable<CatalogResource>> GetByTagsAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        var tagsList = tags.ToList();
        var cacheKey = _keyGenerator.GenerateResourceByTagsKey(tagsList, 1, 100);

        var cachedResources = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedResources != null)
        {
            return cachedResources;
        }

        var resources = await _innerRepository.GetByTagsAsync(tagsList, cancellationToken);
        var resourceList = resources.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            resourceList,
            TimeSpan.FromSeconds(_config.ResourceCacheTtlSeconds),
            cancellationToken);

        return resourceList;
    }

    public Task<IEnumerable<CatalogResource>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        return _innerRepository.SearchAsync(query, cancellationToken);
    }

    public async Task<CatalogResource> CreateAsync(CatalogResource resource, CancellationToken cancellationToken = default)
    {
        var createdResource = await _innerRepository.CreateAsync(resource, cancellationToken);

        await InvalidateRelatedCachesAsync(createdResource, cancellationToken);

        return createdResource;
    }

    public async Task<CatalogResource> UpdateAsync(CatalogResource resource, CancellationToken cancellationToken = default)
    {
        var updatedResource = await _innerRepository.UpdateAsync(resource, cancellationToken);

        await InvalidateRelatedCachesAsync(updatedResource, cancellationToken);

        return updatedResource;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var resource = await GetByIdAsync(id, cancellationToken);

        var result = await _innerRepository.DeleteAsync(id, cancellationToken);

        if (result && resource != null)
        {
            await InvalidateRelatedCachesAsync(resource, cancellationToken);
        }

        return result;
    }

    public async Task<IEnumerable<CatalogResource>> GetAllAsync(
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return await _innerRepository.GetAllAsync(pageSize, pageNumber, cancellationToken);
    }

    private async Task InvalidateRelatedCachesAsync(CatalogResource resource, CancellationToken cancellationToken)
    {
        try
        {
            var invalidationTasks = new List<Task>
            {
                _cacheService.RemoveAsync(_keyGenerator.GenerateResourceKey(resource.Id), cancellationToken),
                _cacheService.RemoveAsync(_keyGenerator.GenerateResourceByNameKey(resource.Name), cancellationToken),
                _cacheService.RemoveByPatternAsync(
                    _keyGenerator.GenerateInvalidationPattern(
                        CacheConstants.Prefixes.Resource,
                        CacheConstants.Operations.GetByType,
                        resource.ResourceType.ToString()),
                    cancellationToken),
                _cacheService.RemoveByPatternAsync(
                    _keyGenerator.GenerateInvalidationPattern(
                        CacheConstants.Prefixes.Resource,
                        CacheConstants.Operations.GetByNamespace,
                        resource.Namespace ?? "*"),
                    cancellationToken)
            };

            if (resource.Tags != null && resource.Tags.Any())
            {
                invalidationTasks.Add(
                    _cacheService.RemoveByPatternAsync(
                        _keyGenerator.GenerateInvalidationPattern(
                            CacheConstants.Prefixes.Resource,
                            CacheConstants.Operations.GetByTags),
                        cancellationToken));
            }

            await Task.WhenAll(invalidationTasks);

            _logger.LogDebug(
                "Invalidated caches for resource {ResourceId} ({ResourceName})",
                resource.Id,
                resource.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to invalidate caches for resource {ResourceId}",
                resource.Id);
        }
    }
}
