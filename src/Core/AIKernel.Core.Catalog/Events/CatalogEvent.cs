using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Events;

public enum CatalogEventType
{
    ResourceCreated,
    ResourceUpdated,
    ResourceDeleted,
    RelationshipCreated,
    RelationshipDeleted
}

public abstract class CatalogEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public CatalogEventType EventType { get; set; }
    public string? UserId { get; set; }
    public string? CorrelationId { get; set; }
}

public class ResourceCreatedEvent : CatalogEvent
{
    public Guid ResourceId { get; set; }
    public ResourceType ResourceType { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? Namespace { get; set; }

    public ResourceCreatedEvent()
    {
        EventType = CatalogEventType.ResourceCreated;
    }
}

public class ResourceUpdatedEvent : CatalogEvent
{
    public Guid ResourceId { get; set; }
    public ResourceType ResourceType { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public Dictionary<string, object> Changes { get; set; } = new();

    public ResourceUpdatedEvent()
    {
        EventType = CatalogEventType.ResourceUpdated;
    }
}

public class ResourceDeletedEvent : CatalogEvent
{
    public Guid ResourceId { get; set; }
    public ResourceType ResourceType { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? Namespace { get; set; }

    public ResourceDeletedEvent()
    {
        EventType = CatalogEventType.ResourceDeleted;
    }
}

public class RelationshipCreatedEvent : CatalogEvent
{
    public Guid RelationshipId { get; set; }
    public Guid SourceResourceId { get; set; }
    public Guid TargetResourceId { get; set; }
    public RelationshipType RelationshipType { get; set; }

    public RelationshipCreatedEvent()
    {
        EventType = CatalogEventType.RelationshipCreated;
    }
}

public class RelationshipDeletedEvent : CatalogEvent
{
    public Guid RelationshipId { get; set; }
    public Guid SourceResourceId { get; set; }
    public Guid TargetResourceId { get; set; }
    public RelationshipType RelationshipType { get; set; }

    public RelationshipDeletedEvent()
    {
        EventType = CatalogEventType.RelationshipDeleted;
    }
}
