using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Contracts.Interfaces;

public interface IResourceRepository
{
    Task<CatalogResource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CatalogResource?> GetByNameAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> GetByTypeAsync(ResourceType resourceType, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> GetByNamespaceAsync(string @namespace, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> GetByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<CatalogResource> CreateAsync(CatalogResource resource, CancellationToken cancellationToken = default);
    Task<CatalogResource> UpdateAsync(CatalogResource resource, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> GetAllAsync(int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default);
}
