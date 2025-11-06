using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Contracts.Models;

public class Relationship
{
    public Guid Id { get; set; }
    public Guid SourceResourceId { get; set; }
    public Guid TargetResourceId { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsBidirectional { get; set; }

    public string? DependencyType { get; set; }
    public bool Required { get; set; }
    public string? VersionConstraint { get; set; }
    public string? TransformationType { get; set; }
    public string? TransformationLogic { get; set; }
}
