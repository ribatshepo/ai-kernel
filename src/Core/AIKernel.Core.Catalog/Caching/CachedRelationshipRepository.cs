using AIKernel.Core.Catalog.Contracts.Enums;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AIKernel.Core.Catalog.Caching;

public class CachedRelationshipRepository : IRelationshipRepository
{
    private readonly IRelationshipRepository _innerRepository;
    private readonly IDistributedCacheService _cacheService;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ICacheMetricsCollector _metricsCollector;
    private readonly ILogger<CachedRelationshipRepository> _logger;
    private readonly CacheDefaultsConfiguration _config;

    public CachedRelationshipRepository(
        IRelationshipRepository innerRepository,
        IDistributedCacheService cacheService,
        ICacheKeyGenerator keyGenerator,
        ICacheMetricsCollector metricsCollector,
        ILogger<CachedRelationshipRepository> logger,
        CacheDefaultsConfiguration config)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<Relationship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateRelationshipKey(id);

        var cachedRelationship = await _cacheService.GetAsync<Relationship>(cacheKey, cancellationToken);

        if (cachedRelationship != null)
        {
            return cachedRelationship;
        }

        var relationship = await _innerRepository.GetByIdAsync(id, cancellationToken);

        if (relationship != null)
        {
            await _cacheService.SetAsync(
                cacheKey,
                relationship,
                TimeSpan.FromSeconds(_config.RelationshipCacheTtlSeconds),
                cancellationToken);
        }

        return relationship;
    }

    public Task<IEnumerable<Relationship>> GetBySourceAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        return _innerRepository.GetBySourceAsync(sourceId, cancellationToken);
    }

    public Task<IEnumerable<Relationship>> GetByTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
    {
        return _innerRepository.GetByTargetAsync(targetId, cancellationToken);
    }

    public Task<IEnumerable<Relationship>> GetByTypeAsync(RelationshipType relationshipType, CancellationToken cancellationToken = default)
    {
        return _innerRepository.GetByTypeAsync(relationshipType, cancellationToken);
    }

    public Task<IEnumerable<Relationship>> GetBetweenAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        return _innerRepository.GetBetweenAsync(sourceId, targetId, cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> GetDependenciesAsync(
        Guid resourceId,
        int depth = 1,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateDependenciesKey(resourceId, depth);

        var cachedDependencies = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedDependencies != null)
        {
            return cachedDependencies;
        }

        var dependencies = await _innerRepository.GetDependenciesAsync(resourceId, depth, cancellationToken);
        var dependenciesList = dependencies.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            dependenciesList,
            TimeSpan.FromSeconds(_config.LineageQueryTtlSeconds),
            cancellationToken);

        return dependenciesList;
    }

    public async Task<IEnumerable<CatalogResource>> GetDependentsAsync(
        Guid resourceId,
        int depth = 1,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateDependentsKey(resourceId, depth);

        var cachedDependents = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedDependents != null)
        {
            return cachedDependents;
        }

        var dependents = await _innerRepository.GetDependentsAsync(resourceId, depth, cancellationToken);
        var dependentsList = dependents.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            dependentsList,
            TimeSpan.FromSeconds(_config.LineageQueryTtlSeconds),
            cancellationToken);

        return dependentsList;
    }

    public async Task<IEnumerable<CatalogResource>> GetLineageUpstreamAsync(
        Guid resourceId,
        int depth = 10,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateLineageKey(resourceId, "upstream", depth);

        var cachedLineage = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedLineage != null)
        {
            return cachedLineage;
        }

        var lineage = await _innerRepository.GetLineageUpstreamAsync(resourceId, depth, cancellationToken);
        var lineageList = lineage.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            lineageList,
            TimeSpan.FromSeconds(_config.LineageQueryTtlSeconds),
            cancellationToken);

        return lineageList;
    }

    public async Task<IEnumerable<CatalogResource>> GetLineageDownstreamAsync(
        Guid resourceId,
        int depth = 10,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = _keyGenerator.GenerateLineageKey(resourceId, "downstream", depth);

        var cachedLineage = await _cacheService.GetAsync<List<CatalogResource>>(cacheKey, cancellationToken);

        if (cachedLineage != null)
        {
            return cachedLineage;
        }

        var lineage = await _innerRepository.GetLineageDownstreamAsync(resourceId, depth, cancellationToken);
        var lineageList = lineage.ToList();

        await _cacheService.SetAsync(
            cacheKey,
            lineageList,
            TimeSpan.FromSeconds(_config.LineageQueryTtlSeconds),
            cancellationToken);

        return lineageList;
    }

    public Task<bool> HasCycleAsync(
        Guid sourceId,
        Guid targetId,
        RelationshipType relationshipType,
        CancellationToken cancellationToken = default)
    {
        return _innerRepository.HasCycleAsync(sourceId, targetId, relationshipType, cancellationToken);
    }

    public async Task<Relationship> CreateAsync(Relationship relationship, CancellationToken cancellationToken = default)
    {
        var createdRelationship = await _innerRepository.CreateAsync(relationship, cancellationToken);

        await InvalidateLineageCachesAsync(relationship.SourceResourceId, relationship.TargetResourceId, cancellationToken);

        return createdRelationship;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var relationship = await GetByIdAsync(id, cancellationToken);

        var result = await _innerRepository.DeleteAsync(id, cancellationToken);

        if (result && relationship != null)
        {
            await InvalidateLineageCachesAsync(relationship.SourceResourceId, relationship.TargetResourceId, cancellationToken);
        }

        return result;
    }

    public Task<IEnumerable<Relationship>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _innerRepository.GetAllAsync(cancellationToken);
    }

    private async Task InvalidateLineageCachesAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken)
    {
        try
        {
            var invalidationTasks = new List<Task>
            {
                _cacheService.RemoveByPatternAsync(
                    _keyGenerator.GenerateInvalidationPattern(
                        CacheConstants.Prefixes.Lineage,
                        "*",
                        sourceId.ToString()),
                    cancellationToken),
                _cacheService.RemoveByPatternAsync(
                    _keyGenerator.GenerateInvalidationPattern(
                        CacheConstants.Prefixes.Lineage,
                        "*",
                        targetId.ToString()),
                    cancellationToken)
            };

            await Task.WhenAll(invalidationTasks);

            _logger.LogDebug(
                "Invalidated lineage caches for resources {SourceId} and {TargetId}",
                sourceId,
                targetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to invalidate lineage caches for resources {SourceId} and {TargetId}",
                sourceId,
                targetId);
        }
    }
}
