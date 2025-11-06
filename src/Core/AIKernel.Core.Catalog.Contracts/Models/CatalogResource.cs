using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Contracts.Models;

public class CatalogResource
{
    public Guid Id { get; set; }
    public ResourceType ResourceType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string Version { get; set; } = "1.0.0";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Dictionary<string, string> Properties { get; set; } = new();

    public List<Guid> DependencyIds { get; set; } = new();
    public List<Guid> UpstreamLineageIds { get; set; } = new();
    public List<Guid> DownstreamLineageIds { get; set; } = new();
}
