using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Contracts.Interfaces;

/// <summary>
/// Unified catalog service that coordinates operations across multiple data stores
/// </summary>
public interface ICatalogService
{
    // Resource Management

    /// <summary>
    /// Retrieves a catalog resource by its unique identifier
    /// </summary>
    Task<CatalogResource?> GetResourceByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a catalog resource by name and optional namespace
    /// </summary>
    Task<CatalogResource?> GetResourceByNameAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all resources of a specific type
    /// </summary>
    Task<IEnumerable<CatalogResource>> GetResourcesByTypeAsync(ResourceType resourceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all resources in a specific namespace
    /// </summary>
    Task<IEnumerable<CatalogResource>> GetResourcesByNamespaceAsync(string @namespace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves resources that have any of the specified tags
    /// </summary>
    Task<IEnumerable<CatalogResource>> GetResourcesByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs full-text search across catalog resources
    /// </summary>
    Task<IEnumerable<CatalogResource>> SearchResourcesAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provides autocomplete suggestions for resource names
    /// </summary>
    Task<IEnumerable<CatalogResource>> AutocompleteAsync(string prefix, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new resource in the catalog
    /// </summary>
    Task<CatalogResource> RegisterResourceAsync(CatalogResource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing catalog resource
    /// </summary>
    Task<CatalogResource> UpdateResourceAsync(CatalogResource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a resource from the catalog
    /// </summary>
    Task<bool> DeleteResourceAsync(Guid id, CancellationToken cancellationToken = default);

    // Relationship Management

    /// <summary>
    /// Retrieves a relationship by its unique identifier
    /// </summary>
    Task<Relationship?> GetRelationshipByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all relationships originating from a source resource
    /// </summary>
    Task<IEnumerable<Relationship>> GetRelationshipsBySourceAsync(Guid sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all relationships targeting a specific resource
    /// </summary>
    Task<IEnumerable<Relationship>> GetRelationshipsByTargetAsync(Guid targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all relationships of a specific type
    /// </summary>
    Task<IEnumerable<Relationship>> GetRelationshipsByTypeAsync(RelationshipType relationshipType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all relationships between two specific resources
    /// </summary>
    Task<IEnumerable<Relationship>> GetRelationshipsBetweenAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new relationship between resources with cycle detection
    /// </summary>
    Task<Relationship> CreateRelationshipAsync(Relationship relationship, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a relationship
    /// </summary>
    Task<bool> DeleteRelationshipAsync(Guid id, CancellationToken cancellationToken = default);

    // Graph Traversal and Lineage

    /// <summary>
    /// Retrieves resources that the specified resource depends on
    /// </summary>
    Task<IEnumerable<CatalogResource>> GetDependenciesAsync(Guid resourceId, int depth = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves resources that depend on the specified resource
    /// </summary>
    Task<IEnumerable<CatalogResource>> GetDependentsAsync(Guid resourceId, int depth = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Traces data lineage upstream to find source resources
    /// </summary>
    Task<IEnumerable<CatalogResource>> GetLineageUpstreamAsync(Guid resourceId, int depth = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Traces data lineage downstream to find consumer resources
    /// </summary>
    Task<IEnumerable<CatalogResource>> GetLineageDownstreamAsync(Guid resourceId, int depth = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if creating a relationship would introduce a cycle
    /// </summary>
    Task<bool> WouldCreateCycleAsync(Guid sourceId, Guid targetId, RelationshipType relationshipType, CancellationToken cancellationToken = default);

    // Bulk Operations and Synchronization

    /// <summary>
    /// Synchronizes the search index with the primary data store
    /// </summary>
    Task<int> SynchronizeSearchIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves facet counts for filtering
    /// </summary>
    Task<Dictionary<string, long>> GetFacetsAsync(string? query = null, CancellationToken cancellationToken = default);
}
