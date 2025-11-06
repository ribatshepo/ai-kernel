using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIKernel.Core.Catalog.Persistence.PostgreSQL.Entities;

[Table("properties", Schema = "metadata")]
public class PropertyEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("resource_id")]
    public Guid ResourceId { get; set; }

    [Required]
    [Column("property_key")]
    [MaxLength(255)]
    public string PropertyKey { get; set; } = string.Empty;

    [Column("property_value")]
    public string? PropertyValue { get; set; }

    [Column("property_type")]
    [MaxLength(50)]
    public string? PropertyType { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ResourceId")]
    public ResourceEntity? Resource { get; set; }
}
