using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Contracts.Interfaces;

public interface ISearchRepository
{
    /// <summary>
    /// Performs full-text search across catalog resources
    /// </summary>
    Task<IEnumerable<CatalogResource>> SearchAsync(
        string query,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provides autocomplete suggestions based on resource names
    /// </summary>
    Task<IEnumerable<CatalogResource>> AutocompleteAsync(
        string prefix,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches resources filtered by resource type
    /// </summary>
    Task<IEnumerable<CatalogResource>> SearchByTypeAsync(
        ResourceType resourceType,
        string? query = null,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches resources filtered by namespace
    /// </summary>
    Task<IEnumerable<CatalogResource>> SearchByNamespaceAsync(
        string @namespace,
        string? query = null,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches resources by tags with optional match-all mode
    /// </summary>
    Task<IEnumerable<CatalogResource>> SearchByTagsAsync(
        IEnumerable<string> tags,
        bool matchAll = false,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves facet counts for resource types, namespaces, and tags
    /// </summary>
    Task<Dictionary<string, long>> GetFacetsAsync(
        string? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a single resource into the search index
    /// </summary>
    Task IndexAsync(CatalogResource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk indexes multiple resources into the search index
    /// </summary>
    Task BulkIndexAsync(IEnumerable<CatalogResource> resources, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a resource from the search index
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recreates the index and reindexes all provided resources
    /// </summary>
    Task ReindexAllAsync(IEnumerable<CatalogResource> resources, CancellationToken cancellationToken = default);
}
