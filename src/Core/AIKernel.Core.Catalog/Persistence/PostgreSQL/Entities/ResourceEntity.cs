using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Persistence.PostgreSQL.Entities;

[Table("resources", Schema = "catalog")]
public class ResourceEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("resource_type")]
    [MaxLength(100)]
    public string ResourceType { get; set; } = string.Empty;

    [Required]
    [Column("resource_name")]
    [MaxLength(255)]
    public string ResourceName { get; set; } = string.Empty;

    [Column("resource_namespace")]
    [MaxLength(255)]
    public string? ResourceNamespace { get; set; }

    [Column("metadata")]
    public string Metadata { get; set; } = "{}";

    [Column("tags")]
    public string Tags { get; set; } = "[]";

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("created_by")]
    [MaxLength(255)]
    public string? CreatedBy { get; set; }

    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("semantic_version")]
    [MaxLength(50)]
    public string SemanticVersion { get; set; } = "1.0.0";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    public ICollection<PropertyEntity> Properties { get; set; } = new List<PropertyEntity>();
    public ICollection<ResourceVersionEntity> Versions { get; set; } = new List<ResourceVersionEntity>();
}
