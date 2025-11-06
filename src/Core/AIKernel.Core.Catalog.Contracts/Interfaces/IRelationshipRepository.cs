using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Contracts.Interfaces;

public interface IRelationshipRepository
{
    Task<Relationship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Relationship>> GetBySourceAsync(Guid sourceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Relationship>> GetByTargetAsync(Guid targetId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Relationship>> GetByTypeAsync(RelationshipType relationshipType, CancellationToken cancellationToken = default);
    Task<IEnumerable<Relationship>> GetBetweenAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> GetDependenciesAsync(Guid resourceId, int depth = 1, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> GetDependentsAsync(Guid resourceId, int depth = 1, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> GetLineageUpstreamAsync(Guid resourceId, int depth = 10, CancellationToken cancellationToken = default);
    Task<IEnumerable<CatalogResource>> GetLineageDownstreamAsync(Guid resourceId, int depth = 10, CancellationToken cancellationToken = default);
    Task<bool> HasCycleAsync(Guid sourceId, Guid targetId, RelationshipType relationshipType, CancellationToken cancellationToken = default);
    Task<Relationship> CreateAsync(Relationship relationship, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Relationship>> GetAllAsync(CancellationToken cancellationToken = default);
}
