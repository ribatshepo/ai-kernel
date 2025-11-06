using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AIKernel.Core.Catalog.Persistence.PostgreSQL;

/// <summary>
/// Design-time factory for EF Core migrations
/// </summary>
public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();

        // Use a default connection string for migrations
        // This can be overridden at runtime via appsettings.json
        var connectionString = "Host=localhost;Port=5432;Database=aikernel_catalog;Username=aikernel_user;Password=aikernel_pass";

        optionsBuilder.UseNpgsql(connectionString);

        return new CatalogDbContext(optionsBuilder.Options);
    }
}
