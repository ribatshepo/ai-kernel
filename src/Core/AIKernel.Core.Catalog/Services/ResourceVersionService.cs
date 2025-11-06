using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Persistence.PostgreSQL;
using AIKernel.Core.Catalog.Persistence.PostgreSQL.Entities;

namespace AIKernel.Core.Catalog.Services;

/// <summary>
/// Manages version control for catalog resources
/// </summary>
public class ResourceVersionService
{
    private readonly CatalogDbContext _context;
    private readonly ILogger<ResourceVersionService> _logger;

    public ResourceVersionService(CatalogDbContext context, ILogger<ResourceVersionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new version snapshot of a resource
    /// </summary>
    public async Task<ResourceVersionEntity> CreateVersionAsync(
        CatalogResource resource,
        string changedBy,
        string? changeReason = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating version snapshot for resource {ResourceId}", resource.Id);

        // Get the latest version number
        var latestVersion = await _context.ResourceVersions
            .Where(v => v.ResourceId == resource.Id)
            .OrderByDescending(v => v.Version)
            .Select(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var newVersion = latestVersion + 1;

        // Create version snapshot
        var versionEntity = new ResourceVersionEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = resource.Id,
            Version = newVersion,
            Metadata = JsonSerializer.Serialize(new
            {
                resource.Name,
                resource.Namespace,
                resource.ResourceType,
                resource.Version,
                resource.Tags,
                resource.Metadata,
                resource.Properties,
                resource.IsActive,
                resource.UpdatedAt
            }),
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow,
            ChangeReason = changeReason
        };

        _context.ResourceVersions.Add(versionEntity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created version {Version} for resource {ResourceId}", newVersion, resource.Id);

        return versionEntity;
    }

    /// <summary>
    /// Gets all versions of a resource
    /// </summary>
    public async Task<IEnumerable<ResourceVersionEntity>> GetVersionsAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ResourceVersions
            .Where(v => v.ResourceId == resourceId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a specific version of a resource
    /// </summary>
    public async Task<ResourceVersionEntity?> GetVersionAsync(
        Guid resourceId,
        int version,
        CancellationToken cancellationToken = default)
    {
        return await _context.ResourceVersions
            .FirstOrDefaultAsync(v => v.ResourceId == resourceId && v.Version == version, cancellationToken);
    }

    /// <summary>
    /// Gets the latest version number for a resource
    /// </summary>
    public async Task<int> GetLatestVersionNumberAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ResourceVersions
            .Where(v => v.ResourceId == resourceId)
            .OrderByDescending(v => v.Version)
            .Select(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the version history for a resource with pagination
    /// </summary>
    public async Task<VersionHistory> GetVersionHistoryAsync(
        Guid resourceId,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ResourceVersions
            .Where(v => v.ResourceId == resourceId)
            .OrderByDescending(v => v.Version);

        var totalCount = await query.CountAsync(cancellationToken);

        var versions = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new VersionHistory
        {
            ResourceId = resourceId,
            TotalVersions = totalCount,
            Versions = versions,
            PageSize = pageSize,
            PageNumber = pageNumber,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    /// <summary>
    /// Compares two versions of a resource
    /// </summary>
    public async Task<VersionComparison?> CompareVersionsAsync(
        Guid resourceId,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
    {
        var fromVersionEntity = await GetVersionAsync(resourceId, fromVersion, cancellationToken);
        var toVersionEntity = await GetVersionAsync(resourceId, toVersion, cancellationToken);

        if (fromVersionEntity == null || toVersionEntity == null)
            return null;

        var fromMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(fromVersionEntity.Metadata);
        var toMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(toVersionEntity.Metadata);

        return new VersionComparison
        {
            ResourceId = resourceId,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            FromMetadata = fromMetadata ?? new Dictionary<string, object>(),
            ToMetadata = toMetadata ?? new Dictionary<string, object>(),
            ChangedBy = toVersionEntity.ChangedBy,
            ChangedAt = toVersionEntity.ChangedAt,
            ChangeReason = toVersionEntity.ChangeReason
        };
    }

    /// <summary>
    /// Deletes old versions beyond a retention limit
    /// </summary>
    public async Task<int> PruneOldVersionsAsync(
        Guid resourceId,
        int keepLatestCount = 10,
        CancellationToken cancellationToken = default)
    {
        var versionsToDelete = await _context.ResourceVersions
            .Where(v => v.ResourceId == resourceId)
            .OrderByDescending(v => v.Version)
            .Skip(keepLatestCount)
            .ToListAsync(cancellationToken);

        if (!versionsToDelete.Any())
            return 0;

        _context.ResourceVersions.RemoveRange(versionsToDelete);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pruned {Count} old versions for resource {ResourceId}", versionsToDelete.Count, resourceId);

        return versionsToDelete.Count;
    }
}

public class VersionHistory
{
    public Guid ResourceId { get; set; }
    public int TotalVersions { get; set; }
    public IEnumerable<ResourceVersionEntity> Versions { get; set; } = Array.Empty<ResourceVersionEntity>();
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
}

public class VersionComparison
{
    public Guid ResourceId { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public Dictionary<string, object> FromMetadata { get; set; } = new();
    public Dictionary<string, object> ToMetadata { get; set; } = new();
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? ChangeReason { get; set; }
}
