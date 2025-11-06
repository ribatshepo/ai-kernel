using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIKernel.Core.Catalog.Persistence.PostgreSQL.Entities;

[Table("resource_versions", Schema = "catalog")]
public class ResourceVersionEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("resource_id")]
    public Guid ResourceId { get; set; }

    [Required]
    [Column("version")]
    public int Version { get; set; }

    [Required]
    [Column("metadata")]
    public string Metadata { get; set; } = "{}";

    [Column("changed_by")]
    [MaxLength(255)]
    public string? ChangedBy { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; }

    [Column("change_reason")]
    public string? ChangeReason { get; set; }

    [ForeignKey("ResourceId")]
    public ResourceEntity? Resource { get; set; }
}
