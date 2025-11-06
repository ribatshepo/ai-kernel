using Neo4j.Driver;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Persistence.Neo4j;

public class Neo4jGraphRepository : IRelationshipRepository, IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly string _database;

    public Neo4jGraphRepository(IDriver driver, string database = "neo4j")
    {
        _driver = driver;
        _database = database;
    }

    public async Task<Relationship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH ()-[r]-()
                  WHERE r.id = $id
                  RETURN r, startNode(r).id as sourceId, endNode(r).id as targetId",
                new { id = id.ToString() });

            if (await cursor.FetchAsync())
            {
                var rel = cursor.Current["r"].As<IRelationship>();
                var sourceId = Guid.Parse(cursor.Current["sourceId"].As<string>());
                var targetId = Guid.Parse(cursor.Current["targetId"].As<string>());

                return MapToRelationship(rel, sourceId, targetId);
            }

            return null;
        });

        return result;
    }

    public async Task<IEnumerable<Relationship>> GetBySourceAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH (source:Resource {id: $sourceId})-[r]->(target:Resource)
                  RETURN r, source.id as sourceId, target.id as targetId",
                new { sourceId = sourceId.ToString() });

            var relationships = new List<Relationship>();
            while (await cursor.FetchAsync())
            {
                var rel = cursor.Current["r"].As<IRelationship>();
                var sid = Guid.Parse(cursor.Current["sourceId"].As<string>());
                var tid = Guid.Parse(cursor.Current["targetId"].As<string>());

                relationships.Add(MapToRelationship(rel, sid, tid));
            }

            return relationships;
        });

        return result;
    }

    public async Task<IEnumerable<Relationship>> GetByTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH (source:Resource)-[r]->(target:Resource {id: $targetId})
                  RETURN r, source.id as sourceId, target.id as targetId",
                new { targetId = targetId.ToString() });

            var relationships = new List<Relationship>();
            while (await cursor.FetchAsync())
            {
                var rel = cursor.Current["r"].As<IRelationship>();
                var sid = Guid.Parse(cursor.Current["sourceId"].As<string>());
                var tid = Guid.Parse(cursor.Current["targetId"].As<string>());

                relationships.Add(MapToRelationship(rel, sid, tid));
            }

            return relationships;
        });

        return result;
    }

    public async Task<IEnumerable<Relationship>> GetByTypeAsync(RelationshipType relationshipType, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var relType = ConvertRelationshipType(relationshipType);
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"MATCH (source:Resource)-[r:{relType}]->(target:Resource)
                   RETURN r, source.id as sourceId, target.id as targetId");

            var relationships = new List<Relationship>();
            while (await cursor.FetchAsync())
            {
                var rel = cursor.Current["r"].As<IRelationship>();
                var sid = Guid.Parse(cursor.Current["sourceId"].As<string>());
                var tid = Guid.Parse(cursor.Current["targetId"].As<string>());

                relationships.Add(MapToRelationship(rel, sid, tid));
            }

            return relationships;
        });

        return result;
    }

    public async Task<IEnumerable<Relationship>> GetBetweenAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH (source:Resource {id: $sourceId})-[r]->(target:Resource {id: $targetId})
                  RETURN r, source.id as sourceId, target.id as targetId",
                new { sourceId = sourceId.ToString(), targetId = targetId.ToString() });

            var relationships = new List<Relationship>();
            while (await cursor.FetchAsync())
            {
                var rel = cursor.Current["r"].As<IRelationship>();
                var sid = Guid.Parse(cursor.Current["sourceId"].As<string>());
                var tid = Guid.Parse(cursor.Current["targetId"].As<string>());

                relationships.Add(MapToRelationship(rel, sid, tid));
            }

            return relationships;
        });

        return result;
    }

    public async Task<IEnumerable<CatalogResource>> GetDependenciesAsync(Guid resourceId, int depth = 1, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"MATCH (source:Resource {{id: $resourceId}})-[:DEPENDS_ON*1..{depth}]->(dep:Resource)
                   RETURN DISTINCT dep",
                new { resourceId = resourceId.ToString() });

            var resources = new List<CatalogResource>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["dep"].As<INode>();
                resources.Add(MapToCatalogResource(node));
            }

            return resources;
        });

        return result;
    }

    public async Task<IEnumerable<CatalogResource>> GetDependentsAsync(Guid resourceId, int depth = 1, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"MATCH (source:Resource {{id: $resourceId}})<-[:DEPENDS_ON*1..{depth}]-(dependent:Resource)
                   RETURN DISTINCT dependent",
                new { resourceId = resourceId.ToString() });

            var resources = new List<CatalogResource>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["dependent"].As<INode>();
                resources.Add(MapToCatalogResource(node));
            }

            return resources;
        });

        return result;
    }

    public async Task<IEnumerable<CatalogResource>> GetLineageUpstreamAsync(Guid resourceId, int depth = 10, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"MATCH (source:Resource {{id: $resourceId}})<-[:PRODUCES*1..{depth}]-(upstream:Resource)
                   RETURN DISTINCT upstream",
                new { resourceId = resourceId.ToString() });

            var resources = new List<CatalogResource>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["upstream"].As<INode>();
                resources.Add(MapToCatalogResource(node));
            }

            return resources;
        });

        return result;
    }

    public async Task<IEnumerable<CatalogResource>> GetLineageDownstreamAsync(Guid resourceId, int depth = 10, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"MATCH (source:Resource {{id: $resourceId}})-[:PRODUCES*1..{depth}]->(downstream:Resource)
                   RETURN DISTINCT downstream",
                new { resourceId = resourceId.ToString() });

            var resources = new List<CatalogResource>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["downstream"].As<INode>();
                resources.Add(MapToCatalogResource(node));
            }

            return resources;
        });

        return result;
    }

    public async Task<bool> HasCycleAsync(Guid sourceId, Guid targetId, RelationshipType relationshipType, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var relType = ConvertRelationshipType(relationshipType);
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"MATCH path = (source:Resource {{id: $targetId}})-[:{relType}*1..50]->(target:Resource {{id: $sourceId}})
                   RETURN count(path) > 0 as hasCycle",
                new { sourceId = sourceId.ToString(), targetId = targetId.ToString() });

            if (await cursor.FetchAsync())
            {
                return cursor.Current["hasCycle"].As<bool>();
            }

            return false;
        });

        return result;
    }

    public async Task<Relationship> CreateAsync(Relationship relationship, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var relType = ConvertRelationshipType(relationship.RelationshipType);
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                @"MERGE (source:Resource {id: $sourceId})
                  MERGE (target:Resource {id: $targetId})
                  CREATE (source)-[r:" + relType + @" {
                      id: $id,
                      created_at: datetime(),
                      created_by: $createdBy,
                      is_bidirectional: $isBidirectional,
                      dependency_type: $dependencyType,
                      required: $required,
                      version_constraint: $versionConstraint,
                      transformation_type: $transformationType,
                      transformation_logic: $transformationLogic
                  }]->(target)
                  RETURN r",
                new
                {
                    sourceId = relationship.SourceResourceId.ToString(),
                    targetId = relationship.TargetResourceId.ToString(),
                    id = relationship.Id == Guid.Empty ? Guid.NewGuid().ToString() : relationship.Id.ToString(),
                    createdBy = relationship.CreatedBy,
                    isBidirectional = relationship.IsBidirectional,
                    dependencyType = relationship.DependencyType,
                    required = relationship.Required,
                    versionConstraint = relationship.VersionConstraint,
                    transformationType = relationship.TransformationType,
                    transformationLogic = relationship.TransformationLogic
                });
        });

        return relationship;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH ()-[r {id: $id}]-()
                  DELETE r
                  RETURN count(r) as deleted",
                new { id = id.ToString() });

            if (await cursor.FetchAsync())
            {
                return cursor.Current["deleted"].As<int>() > 0;
            }

            return false;
        });

        return result;
    }

    public async Task<IEnumerable<Relationship>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_database));

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH (source:Resource)-[r]->(target:Resource)
                  RETURN r, source.id as sourceId, target.id as targetId
                  LIMIT 1000");

            var relationships = new List<Relationship>();
            while (await cursor.FetchAsync())
            {
                var rel = cursor.Current["r"].As<IRelationship>();
                var sid = Guid.Parse(cursor.Current["sourceId"].As<string>());
                var tid = Guid.Parse(cursor.Current["targetId"].As<string>());

                relationships.Add(MapToRelationship(rel, sid, tid));
            }

            return relationships;
        });

        return result;
    }

    private static Relationship MapToRelationship(IRelationship rel, Guid sourceId, Guid targetId)
    {
        return new Relationship
        {
            Id = Guid.Parse(rel.Properties["id"].As<string>()),
            SourceResourceId = sourceId,
            TargetResourceId = targetId,
            RelationshipType = ConvertToRelationshipType(rel.Type),
            CreatedAt = rel.Properties.ContainsKey("created_at")
                ? rel.Properties["created_at"].As<DateTime>()
                : DateTime.UtcNow,
            CreatedBy = rel.Properties.ContainsKey("created_by")
                ? rel.Properties["created_by"].As<string>()
                : null,
            IsBidirectional = rel.Properties.ContainsKey("is_bidirectional")
                ? rel.Properties["is_bidirectional"].As<bool>()
                : false,
            DependencyType = rel.Properties.ContainsKey("dependency_type")
                ? rel.Properties["dependency_type"].As<string>()
                : null,
            Required = rel.Properties.ContainsKey("required")
                ? rel.Properties["required"].As<bool>()
                : false,
            VersionConstraint = rel.Properties.ContainsKey("version_constraint")
                ? rel.Properties["version_constraint"].As<string>()
                : null,
            TransformationType = rel.Properties.ContainsKey("transformation_type")
                ? rel.Properties["transformation_type"].As<string>()
                : null,
            TransformationLogic = rel.Properties.ContainsKey("transformation_logic")
                ? rel.Properties["transformation_logic"].As<string>()
                : null
        };
    }

    private static CatalogResource MapToCatalogResource(INode node)
    {
        return new CatalogResource
        {
            Id = Guid.Parse(node.Properties["id"].As<string>()),
            ResourceType = Enum.Parse<ResourceType>(node.Properties["resource_type"].As<string>()),
            Name = node.Properties["name"].As<string>(),
            Namespace = node.Properties.ContainsKey("namespace")
                ? node.Properties["namespace"].As<string>()
                : null,
            Version = node.Properties.ContainsKey("version")
                ? node.Properties["version"].As<string>()
                : "1.0.0",
            CreatedAt = node.Properties.ContainsKey("created_at")
                ? node.Properties["created_at"].As<DateTime>()
                : DateTime.UtcNow,
            UpdatedAt = node.Properties.ContainsKey("updated_at")
                ? node.Properties["updated_at"].As<DateTime>()
                : DateTime.UtcNow,
            IsActive = node.Properties.ContainsKey("is_active")
                ? node.Properties["is_active"].As<bool>()
                : true
        };
    }

    private static string ConvertRelationshipType(RelationshipType type)
    {
        return type switch
        {
            RelationshipType.DependsOn => "DEPENDS_ON",
            RelationshipType.Produces => "PRODUCES",
            RelationshipType.Consumes => "CONSUMES",
            RelationshipType.Contains => "CONTAINS",
            RelationshipType.TrainedWith => "TRAINED_WITH",
            RelationshipType.HasAccess => "HAS_ACCESS",
            RelationshipType.DerivesFrom => "DERIVES_FROM",
            RelationshipType.References => "REFERENCES",
            RelationshipType.Extends => "EXTENDS",
            _ => "UNKNOWN"
        };
    }

    private static RelationshipType ConvertToRelationshipType(string type)
    {
        return type switch
        {
            "DEPENDS_ON" => RelationshipType.DependsOn,
            "PRODUCES" => RelationshipType.Produces,
            "CONSUMES" => RelationshipType.Consumes,
            "CONTAINS" => RelationshipType.Contains,
            "TRAINED_WITH" => RelationshipType.TrainedWith,
            "HAS_ACCESS" => RelationshipType.HasAccess,
            "DERIVES_FROM" => RelationshipType.DerivesFrom,
            "REFERENCES" => RelationshipType.References,
            "EXTENDS" => RelationshipType.Extends,
            _ => RelationshipType.Unknown
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _driver.DisposeAsync();
    }
}
