using Microsoft.Extensions.Logging;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;
using AIKernel.Core.Catalog.Validation;
using AIKernel.Core.Catalog.Events;

namespace AIKernel.Core.Catalog.Services;

/// <summary>
/// Coordinates catalog operations across multiple data stores (PostgreSQL, Neo4j, Elasticsearch)
/// Ensures eventual consistency between relational metadata, graph relationships, and search indices
/// </summary>
public class CatalogService : ICatalogService
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IRelationshipRepository _relationshipRepository;
    private readonly ISearchRepository _searchRepository;
    private readonly ResourceSchemaValidator _validator;
    private readonly ICatalogEventPublisher? _eventPublisher;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(
        IResourceRepository resourceRepository,
        IRelationshipRepository relationshipRepository,
        ISearchRepository searchRepository,
        ILogger<CatalogService> logger,
        ICatalogEventPublisher? eventPublisher = null)
    {
        _resourceRepository = resourceRepository;
        _relationshipRepository = relationshipRepository;
        _searchRepository = searchRepository;
        _validator = new ResourceSchemaValidator();
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    // Resource Management

    public async Task<CatalogResource?> GetResourceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _resourceRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<CatalogResource?> GetResourceByNameAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        return await _resourceRepository.GetByNameAsync(name, @namespace, cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> GetResourcesByTypeAsync(ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        return await _resourceRepository.GetByTypeAsync(resourceType, cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> GetResourcesByNamespaceAsync(string @namespace, CancellationToken cancellationToken = default)
    {
        return await _resourceRepository.GetByNamespaceAsync(@namespace, cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> GetResourcesByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        return await _resourceRepository.GetByTagsAsync(tags, cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> SearchResourcesAsync(string query, CancellationToken cancellationToken = default)
    {
        // Use Elasticsearch for full-text search
        return await _searchRepository.SearchAsync(query, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> AutocompleteAsync(string prefix, int limit = 10, CancellationToken cancellationToken = default)
    {
        return await _searchRepository.AutocompleteAsync(prefix, limit, cancellationToken);
    }

    public async Task<CatalogResource> RegisterResourceAsync(CatalogResource resource, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering resource: {ResourceType} - {Name} in namespace {Namespace}",
            resource.ResourceType, resource.Name, resource.Namespace);

        // Validate resource schema
        var validationResult = _validator.Validate(resource);
        if (!validationResult.IsValid)
        {
            var errorMessage = validationResult.GetErrorMessage();
            _logger.LogWarning("Resource validation failed: {Errors}", errorMessage);
            throw new InvalidOperationException($"Resource validation failed: {errorMessage}");
        }

        if (validationResult.Warnings.Any())
        {
            _logger.LogWarning("Resource validation warnings: {Warnings}", validationResult.GetWarningMessage());
        }

        CatalogResource? createdResource = null;
        var rollbackActions = new List<Func<Task>>();

        try
        {
            // 1. Create resource in PostgreSQL (source of truth for metadata)
            createdResource = await _resourceRepository.CreateAsync(resource, cancellationToken);
            rollbackActions.Add(async () =>
            {
                await _resourceRepository.DeleteAsync(createdResource.Id, cancellationToken);
                _logger.LogWarning("Rolled back PostgreSQL resource: {Id}", createdResource.Id);
            });

            // 2. Index resource in Elasticsearch for search
            await _searchRepository.IndexAsync(createdResource, cancellationToken);
            rollbackActions.Add(async () =>
            {
                await _searchRepository.DeleteAsync(createdResource.Id, cancellationToken);
                _logger.LogWarning("Rolled back Elasticsearch index: {Id}", createdResource.Id);
            });

            // 3. Publish event
            if (_eventPublisher != null)
            {
                await _eventPublisher.PublishAsync(new ResourceCreatedEvent
                {
                    ResourceId = createdResource.Id,
                    ResourceType = createdResource.ResourceType,
                    ResourceName = createdResource.Name,
                    Namespace = createdResource.Namespace
                }, cancellationToken);
            }

            _logger.LogInformation("Successfully registered resource: {Id}", createdResource.Id);
            return createdResource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register resource: {ResourceType} - {Name}. Initiating rollback.",
                resource.ResourceType, resource.Name);

            // Execute rollback actions in reverse order
            for (int i = rollbackActions.Count - 1; i >= 0; i--)
            {
                try
                {
                    await rollbackActions[i]();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback action {Index} failed during resource registration cleanup", i);
                }
            }

            throw;
        }
    }

    public async Task<CatalogResource> UpdateResourceAsync(CatalogResource resource, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating resource: {Id}", resource.Id);

        // Get existing resource for validation
        var existingResource = await _resourceRepository.GetByIdAsync(resource.Id, cancellationToken);
        if (existingResource == null)
        {
            throw new InvalidOperationException($"Resource {resource.Id} not found");
        }

        // Validate update
        var validationResult = _validator.ValidateUpdate(existingResource, resource);
        if (!validationResult.IsValid)
        {
            var errorMessage = validationResult.GetErrorMessage();
            _logger.LogWarning("Resource update validation failed: {Errors}", errorMessage);
            throw new InvalidOperationException($"Resource update validation failed: {errorMessage}");
        }

        if (validationResult.Warnings.Any())
        {
            _logger.LogWarning("Resource update validation warnings: {Warnings}", validationResult.GetWarningMessage());
        }

        try
        {
            // 1. Update resource in PostgreSQL
            var updatedResource = await _resourceRepository.UpdateAsync(resource, cancellationToken);

            // 2. Update search index
            try
            {
                await _searchRepository.IndexAsync(updatedResource, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update Elasticsearch index for resource {Id}. Index may be stale.", resource.Id);
                // Continue - search index staleness is acceptable
            }

            // 3. Publish event
            if (_eventPublisher != null)
            {
                await _eventPublisher.PublishAsync(new ResourceUpdatedEvent
                {
                    ResourceId = updatedResource.Id,
                    ResourceType = updatedResource.ResourceType,
                    ResourceName = updatedResource.Name,
                    Namespace = updatedResource.Namespace
                }, cancellationToken);
            }

            _logger.LogInformation("Successfully updated resource: {Id}", resource.Id);
            return updatedResource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update resource: {Id}", resource.Id);
            throw;
        }
    }

    public async Task<bool> DeleteResourceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting resource: {Id}", id);

        try
        {
            // Get resource info before deletion for event
            var resource = await _resourceRepository.GetByIdAsync(id, cancellationToken);
            if (resource == null)
            {
                _logger.LogWarning("Resource {Id} not found in PostgreSQL", id);
                return false;
            }

            // 1. Delete from PostgreSQL
            var deleted = await _resourceRepository.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                return false;
            }

            // 2. Delete from Elasticsearch (best effort)
            try
            {
                await _searchRepository.DeleteAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete resource {Id} from Elasticsearch", id);
                // Continue - eventual consistency will be maintained
            }

            // 3. Publish event
            if (_eventPublisher != null)
            {
                await _eventPublisher.PublishAsync(new ResourceDeletedEvent
                {
                    ResourceId = resource.Id,
                    ResourceType = resource.ResourceType,
                    ResourceName = resource.Name,
                    Namespace = resource.Namespace
                }, cancellationToken);
            }

            _logger.LogInformation("Successfully deleted resource: {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete resource: {Id}", id);
            throw;
        }
    }

    // Relationship Management

    public async Task<Relationship?> GetRelationshipByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<Relationship>> GetRelationshipsBySourceAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetBySourceAsync(sourceId, cancellationToken);
    }

    public async Task<IEnumerable<Relationship>> GetRelationshipsByTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetByTargetAsync(targetId, cancellationToken);
    }

    public async Task<IEnumerable<Relationship>> GetRelationshipsByTypeAsync(RelationshipType relationshipType, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetByTypeAsync(relationshipType, cancellationToken);
    }

    public async Task<IEnumerable<Relationship>> GetRelationshipsBetweenAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetBetweenAsync(sourceId, targetId, cancellationToken);
    }

    public async Task<Relationship> CreateRelationshipAsync(Relationship relationship, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating relationship: {Type} from {Source} to {Target}",
            relationship.RelationshipType, relationship.SourceResourceId, relationship.TargetResourceId);

        try
        {
            // Check for cycles before creating relationship
            var wouldCreateCycle = await _relationshipRepository.HasCycleAsync(
                relationship.SourceResourceId,
                relationship.TargetResourceId,
                relationship.RelationshipType,
                cancellationToken);

            if (wouldCreateCycle)
            {
                throw new InvalidOperationException(
                    $"Creating relationship would introduce a cycle: {relationship.SourceResourceId} -> {relationship.TargetResourceId}");
            }

            // Create relationship in Neo4j
            var createdRelationship = await _relationshipRepository.CreateAsync(relationship, cancellationToken);

            // Publish event
            if (_eventPublisher != null)
            {
                await _eventPublisher.PublishAsync(new RelationshipCreatedEvent
                {
                    RelationshipId = createdRelationship.Id,
                    SourceResourceId = createdRelationship.SourceResourceId,
                    TargetResourceId = createdRelationship.TargetResourceId,
                    RelationshipType = createdRelationship.RelationshipType
                }, cancellationToken);
            }

            _logger.LogInformation("Successfully created relationship: {Id}", createdRelationship.Id);
            return createdRelationship;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create relationship: {Type} from {Source} to {Target}",
                relationship.RelationshipType, relationship.SourceResourceId, relationship.TargetResourceId);
            throw;
        }
    }

    public async Task<bool> DeleteRelationshipAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting relationship: {Id}", id);

        try
        {
            // Get relationship info before deletion for event
            var relationship = await _relationshipRepository.GetByIdAsync(id, cancellationToken);

            var deleted = await _relationshipRepository.DeleteAsync(id, cancellationToken);

            if (deleted && relationship != null)
            {
                // Publish event
                if (_eventPublisher != null)
                {
                    await _eventPublisher.PublishAsync(new RelationshipDeletedEvent
                    {
                        RelationshipId = relationship.Id,
                        SourceResourceId = relationship.SourceResourceId,
                        TargetResourceId = relationship.TargetResourceId,
                        RelationshipType = relationship.RelationshipType
                    }, cancellationToken);
                }

                _logger.LogInformation("Successfully deleted relationship: {Id}", id);
            }
            else
            {
                _logger.LogWarning("Relationship {Id} not found", id);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete relationship: {Id}", id);
            throw;
        }
    }

    // Graph Traversal and Lineage

    public async Task<IEnumerable<CatalogResource>> GetDependenciesAsync(Guid resourceId, int depth = 1, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetDependenciesAsync(resourceId, depth, cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> GetDependentsAsync(Guid resourceId, int depth = 1, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetDependentsAsync(resourceId, depth, cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> GetLineageUpstreamAsync(Guid resourceId, int depth = 10, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetLineageUpstreamAsync(resourceId, depth, cancellationToken);
    }

    public async Task<IEnumerable<CatalogResource>> GetLineageDownstreamAsync(Guid resourceId, int depth = 10, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.GetLineageDownstreamAsync(resourceId, depth, cancellationToken);
    }

    public async Task<bool> WouldCreateCycleAsync(Guid sourceId, Guid targetId, RelationshipType relationshipType, CancellationToken cancellationToken = default)
    {
        return await _relationshipRepository.HasCycleAsync(sourceId, targetId, relationshipType, cancellationToken);
    }

    // Bulk Operations and Synchronization

    public async Task<int> SynchronizeSearchIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting search index synchronization");

        try
        {
            // Get all resources from PostgreSQL (source of truth)
            var allResources = new List<CatalogResource>();
            int pageNumber = 1;
            const int pageSize = 1000;

            while (true)
            {
                var page = await _resourceRepository.GetAllAsync(pageSize, pageNumber, cancellationToken);
                var pageList = page.ToList();

                if (!pageList.Any())
                    break;

                allResources.AddRange(pageList);
                pageNumber++;
            }

            // Reindex all resources in Elasticsearch
            await _searchRepository.ReindexAllAsync(allResources, cancellationToken);

            _logger.LogInformation("Successfully synchronized {Count} resources to search index", allResources.Count);
            return allResources.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize search index");
            throw;
        }
    }

    public async Task<Dictionary<string, long>> GetFacetsAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        return await _searchRepository.GetFacetsAsync(query, cancellationToken);
    }
}
