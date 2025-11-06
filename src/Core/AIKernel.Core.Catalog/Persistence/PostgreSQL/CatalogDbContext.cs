using Microsoft.EntityFrameworkCore;
using AIKernel.Core.Catalog.Persistence.PostgreSQL.Entities;

namespace AIKernel.Core.Catalog.Persistence.PostgreSQL;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<ResourceEntity> Resources { get; set; } = null!;
    public DbSet<PropertyEntity> Properties { get; set; } = null!;
    public DbSet<ResourceVersionEntity> ResourceVersions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ResourceEntity>(entity =>
        {
            entity.ToTable("resources", "catalog");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ResourceType);
            entity.HasIndex(e => e.ResourceNamespace);
            entity.HasIndex(e => new { e.ResourceType, e.ResourceName, e.ResourceNamespace })
                .IsUnique();

            entity.HasMany(e => e.Properties)
                .WithOne(p => p.Resource)
                .HasForeignKey(p => p.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Versions)
                .WithOne(v => v.Resource)
                .HasForeignKey(v => v.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PropertyEntity>(entity =>
        {
            entity.ToTable("properties", "metadata");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ResourceId);
            entity.HasIndex(e => e.PropertyKey);
            entity.HasIndex(e => new { e.ResourceId, e.PropertyKey })
                .IsUnique();
        });

        modelBuilder.Entity<ResourceVersionEntity>(entity =>
        {
            entity.ToTable("resource_versions", "catalog");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ResourceId);
            entity.HasIndex(e => e.Version);
        });
    }
}
