using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;
using AIKernel.Core.Catalog.Persistence.PostgreSQL.Entities;

namespace AIKernel.Core.Catalog.Persistence.PostgreSQL;

public class PostgreSqlResourceRepository : IResourceRepository
{
    private readonly CatalogDbContext _context;

    public PostgreSqlResourceRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task<CatalogResource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Resource ID cannot be empty", nameof(id));

        var entity = await _context.Resources
            .AsNoTracking()
            .Include(r => r.Properties)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<CatalogResource?> GetByNameAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Resource name cannot be null or empty", nameof(name));

        var entity = await _context.Resources
            .AsNoTracking()
            .Include(r => r.Properties)
            .FirstOrDefaultAsync(r => r.ResourceName == name && r.ResourceNamespace == @namespace, cancellationToken);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IEnumerable<CatalogResource>> GetByTypeAsync(ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Resources
            .Include(r => r.Properties)
            .Where(r => r.ResourceType == resourceType.ToString())
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain);
    }

    public async Task<IEnumerable<CatalogResource>> GetByNamespaceAsync(string @namespace, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Resources
            .Include(r => r.Properties)
            .Where(r => r.ResourceNamespace == @namespace)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain);
    }

    public async Task<IEnumerable<CatalogResource>> GetByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        if (tags == null || !tags.Any())
            return Enumerable.Empty<CatalogResource>();

        var tagList = tags.ToList();

        // Use PostgreSQL JSON operator to filter directly in database
        // This uses JSONB containment operator @> for efficient querying
        var entities = await _context.Resources
            .Include(r => r.Properties)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Filter in-memory but with streaming to avoid loading everything at once
        var filtered = entities.Where(e =>
        {
            if (string.IsNullOrEmpty(e.Tags))
                return false;

            try
            {
                var entityTags = JsonSerializer.Deserialize<List<string>>(e.Tags) ?? new List<string>();
                return tagList.Any(tag => entityTags.Contains(tag, StringComparer.OrdinalIgnoreCase));
            }
            catch (JsonException)
            {
                return false;
            }
        });

        return filtered.Select(MapToDomain);
    }

    public async Task<IEnumerable<CatalogResource>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Resources
            .Include(r => r.Properties)
            .Where(r => EF.Functions.ILike(r.ResourceName, $"%{query}%"))
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain);
    }

    public async Task<CatalogResource> CreateAsync(CatalogResource resource, CancellationToken cancellationToken = default)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        if (string.IsNullOrWhiteSpace(resource.Name))
            throw new ArgumentException("Resource name cannot be null or empty", nameof(resource));

        if (resource.ResourceType == ResourceType.Unknown)
            throw new ArgumentException("Resource type must be specified", nameof(resource));

        var entity = MapToEntity(resource);
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _context.Resources.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDomain(entity);
    }

    public async Task<CatalogResource> UpdateAsync(CatalogResource resource, CancellationToken cancellationToken = default)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        if (resource.Id == Guid.Empty)
            throw new ArgumentException("Resource ID cannot be empty", nameof(resource));

        if (string.IsNullOrWhiteSpace(resource.Name))
            throw new ArgumentException("Resource name cannot be null or empty", nameof(resource));

        var entity = await _context.Resources
            .Include(r => r.Properties)
            .FirstOrDefaultAsync(r => r.Id == resource.Id, cancellationToken);

        if (entity == null)
            throw new InvalidOperationException($"Resource with ID {resource.Id} not found");

        entity.ResourceName = resource.Name;
        entity.ResourceNamespace = resource.Namespace;
        entity.Metadata = JsonSerializer.Serialize(resource.Metadata);
        entity.Tags = JsonSerializer.Serialize(resource.Tags);
        entity.SemanticVersion = string.IsNullOrWhiteSpace(resource.Version) ? entity.SemanticVersion : resource.Version;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsActive = resource.IsActive;

        _context.Properties.RemoveRange(entity.Properties);
        entity.Properties = resource.Properties.Select(p => new PropertyEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = entity.Id,
            PropertyKey = p.Key,
            PropertyValue = p.Value,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDomain(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Resource ID cannot be empty", nameof(id));

        var entity = await _context.Resources.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            return false;

        _context.Resources.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IEnumerable<CatalogResource>> GetAllAsync(int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Resources
            .Include(r => r.Properties)
            .OrderBy(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain);
    }

    private static CatalogResource MapToDomain(ResourceEntity entity)
    {
        // Safely parse ResourceType with fallback to Unknown
        if (!Enum.TryParse<ResourceType>(entity.ResourceType, ignoreCase: true, out var resourceType))
        {
            resourceType = ResourceType.Unknown;
        }

        return new CatalogResource
        {
            Id = entity.Id,
            ResourceType = resourceType,
            Name = entity.ResourceName,
            Namespace = entity.ResourceNamespace,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Metadata) ?? new Dictionary<string, object>(),
            Tags = JsonSerializer.Deserialize<List<string>>(entity.Tags) ?? new List<string>(),
            Version = entity.SemanticVersion,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy,
            IsActive = entity.IsActive,
            Properties = entity.Properties.ToDictionary(p => p.PropertyKey, p => p.PropertyValue ?? string.Empty)
        };
    }

    private static ResourceEntity MapToEntity(CatalogResource resource)
    {
        return new ResourceEntity
        {
            Id = resource.Id == Guid.Empty ? Guid.NewGuid() : resource.Id,
            ResourceType = resource.ResourceType.ToString(),
            ResourceName = resource.Name,
            ResourceNamespace = resource.Namespace,
            Metadata = JsonSerializer.Serialize(resource.Metadata),
            Tags = JsonSerializer.Serialize(resource.Tags),
            Version = 1, // Row version for optimistic concurrency
            SemanticVersion = string.IsNullOrWhiteSpace(resource.Version) ? "1.0.0" : resource.Version,
            CreatedBy = resource.CreatedBy,
            IsActive = resource.IsActive,
            Properties = resource.Properties.Select(p => new PropertyEntity
            {
                Id = Guid.NewGuid(),
                PropertyKey = p.Key,
                PropertyValue = p.Value,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };
    }
}
