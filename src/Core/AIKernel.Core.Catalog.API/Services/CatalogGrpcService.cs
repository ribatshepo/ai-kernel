using Grpc.Core;
using AIKernel.Core.Catalog.API.Grpc;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.API.Services;

public class CatalogGrpcService : Grpc.CatalogService.CatalogServiceBase
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<CatalogGrpcService> _logger;

    public CatalogGrpcService(ICatalogService catalogService, ILogger<CatalogGrpcService> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    // Resource operations

    public override async Task<ResourceResponse> GetResource(GetResourceRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource ID format"));

        var resource = await _catalogService.GetResourceByIdAsync(id, context.CancellationToken);

        if (resource == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Resource {id} not found"));

        return MapToResourceResponse(resource);
    }

    public override async Task<ResourceListResponse> SearchResources(SearchRequest request, ServerCallContext context)
    {
        IEnumerable<CatalogResource> resources;

        if (!string.IsNullOrEmpty(request.ResourceType))
        {
            if (!Enum.TryParse<ResourceType>(request.ResourceType, true, out var resourceType))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource type"));

            resources = await _catalogService.GetResourcesByTypeAsync(resourceType, context.CancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.Namespace))
        {
            resources = await _catalogService.GetResourcesByNamespaceAsync(request.Namespace, context.CancellationToken);
        }
        else if (request.Tags.Count > 0)
        {
            resources = await _catalogService.GetResourcesByTagsAsync(request.Tags, context.CancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.Query))
        {
            resources = await _catalogService.SearchResourcesAsync(request.Query, context.CancellationToken);
        }
        else
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one search criterion must be provided"));
        }

        var response = new ResourceListResponse();
        response.Resources.AddRange(resources.Select(MapToResourceResponse));
        response.TotalCount = resources.Count();

        return response;
    }

    public override async Task<ResourceResponse> RegisterResource(RegisterResourceRequest request, ServerCallContext context)
    {
        if (!Enum.TryParse<ResourceType>(request.ResourceType, true, out var resourceType))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource type"));

        var resource = new CatalogResource
        {
            ResourceType = resourceType,
            Name = request.Name,
            Namespace = string.IsNullOrEmpty(request.Namespace) ? null : request.Namespace,
            Metadata = request.Metadata.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
            Tags = request.Tags.ToList(),
            Version = string.IsNullOrEmpty(request.Version) ? "1.0.0" : request.Version,
            Properties = request.Properties.ToDictionary(kv => kv.Key, kv => kv.Value),
            IsActive = true
        };

        var createdResource = await _catalogService.RegisterResourceAsync(resource, context.CancellationToken);
        return MapToResourceResponse(createdResource);
    }

    public override async Task<ResourceResponse> UpdateResource(UpdateResourceRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource ID format"));

        var existingResource = await _catalogService.GetResourceByIdAsync(id, context.CancellationToken);
        if (existingResource == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Resource {id} not found"));

        existingResource.Name = request.Name;
        existingResource.Namespace = string.IsNullOrEmpty(request.Namespace) ? null : request.Namespace;
        existingResource.Metadata = request.Metadata.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
        existingResource.Tags = request.Tags.ToList();
        existingResource.Properties = request.Properties.ToDictionary(kv => kv.Key, kv => kv.Value);

        var updatedResource = await _catalogService.UpdateResourceAsync(existingResource, context.CancellationToken);
        return MapToResourceResponse(updatedResource);
    }

    public override async Task<DeleteResponse> DeleteResource(DeleteResourceRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource ID format"));

        var deleted = await _catalogService.DeleteResourceAsync(id, context.CancellationToken);

        return new DeleteResponse
        {
            Success = deleted,
            Message = deleted ? "Resource deleted successfully" : "Resource not found"
        };
    }

    public override async Task<ResourceListResponse> AutocompleteResources(AutocompleteRequest request, ServerCallContext context)
    {
        var resources = await _catalogService.AutocompleteAsync(request.Prefix, request.Limit, context.CancellationToken);

        var response = new ResourceListResponse();
        response.Resources.AddRange(resources.Select(MapToResourceResponse));
        response.TotalCount = resources.Count();

        return response;
    }

    // Relationship operations

    public override async Task<RelationshipResponse> GetRelationship(GetRelationshipRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid relationship ID format"));

        var relationship = await _catalogService.GetRelationshipByIdAsync(id, context.CancellationToken);

        if (relationship == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Relationship {id} not found"));

        return MapToRelationshipResponse(relationship);
    }

    public override async Task<RelationshipResponse> CreateRelationship(CreateRelationshipRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SourceResourceId, out var sourceId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid source resource ID format"));

        if (!Guid.TryParse(request.TargetResourceId, out var targetId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid target resource ID format"));

        if (!Enum.TryParse<RelationshipType>(request.RelationshipType, true, out var relationshipType))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid relationship type"));

        var relationship = new Relationship
        {
            SourceResourceId = sourceId,
            TargetResourceId = targetId,
            RelationshipType = relationshipType,
            CreatedBy = string.IsNullOrEmpty(request.CreatedBy) ? null : request.CreatedBy,
            IsBidirectional = request.IsBidirectional,
            DependencyType = string.IsNullOrEmpty(request.DependencyType) ? null : request.DependencyType,
            Required = request.Required,
            VersionConstraint = string.IsNullOrEmpty(request.VersionConstraint) ? null : request.VersionConstraint,
            TransformationType = string.IsNullOrEmpty(request.TransformationType) ? null : request.TransformationType,
            TransformationLogic = string.IsNullOrEmpty(request.TransformationLogic) ? null : request.TransformationLogic
        };

        var createdRelationship = await _catalogService.CreateRelationshipAsync(relationship, context.CancellationToken);
        return MapToRelationshipResponse(createdRelationship);
    }

    public override async Task<DeleteResponse> DeleteRelationship(DeleteRelationshipRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid relationship ID format"));

        var deleted = await _catalogService.DeleteRelationshipAsync(id, context.CancellationToken);

        return new DeleteResponse
        {
            Success = deleted,
            Message = deleted ? "Relationship deleted successfully" : "Relationship not found"
        };
    }

    public override async Task<RelationshipListResponse> GetRelationshipsBySource(GetRelationshipsBySourceRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SourceId, out var sourceId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid source ID format"));

        var relationships = await _catalogService.GetRelationshipsBySourceAsync(sourceId, context.CancellationToken);

        var response = new RelationshipListResponse();
        response.Relationships.AddRange(relationships.Select(MapToRelationshipResponse));

        return response;
    }

    public override async Task<RelationshipListResponse> GetRelationshipsByTarget(GetRelationshipsByTargetRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TargetId, out var targetId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid target ID format"));

        var relationships = await _catalogService.GetRelationshipsByTargetAsync(targetId, context.CancellationToken);

        var response = new RelationshipListResponse();
        response.Relationships.AddRange(relationships.Select(MapToRelationshipResponse));

        return response;
    }

    // Graph traversal operations

    public override async Task<ResourceListResponse> GetDependencies(GetDependenciesRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ResourceId, out var resourceId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource ID format"));

        var dependencies = await _catalogService.GetDependenciesAsync(resourceId, request.Depth, context.CancellationToken);

        var response = new ResourceListResponse();
        response.Resources.AddRange(dependencies.Select(MapToResourceResponse));
        response.TotalCount = dependencies.Count();

        return response;
    }

    public override async Task<ResourceListResponse> GetDependents(GetDependentsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ResourceId, out var resourceId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource ID format"));

        var dependents = await _catalogService.GetDependentsAsync(resourceId, request.Depth, context.CancellationToken);

        var response = new ResourceListResponse();
        response.Resources.AddRange(dependents.Select(MapToResourceResponse));
        response.TotalCount = dependents.Count();

        return response;
    }

    public override async Task<ResourceListResponse> GetLineageUpstream(GetLineageRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ResourceId, out var resourceId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource ID format"));

        var upstream = await _catalogService.GetLineageUpstreamAsync(resourceId, request.Depth, context.CancellationToken);

        var response = new ResourceListResponse();
        response.Resources.AddRange(upstream.Select(MapToResourceResponse));
        response.TotalCount = upstream.Count();

        return response;
    }

    public override async Task<ResourceListResponse> GetLineageDownstream(GetLineageRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ResourceId, out var resourceId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid resource ID format"));

        var downstream = await _catalogService.GetLineageDownstreamAsync(resourceId, request.Depth, context.CancellationToken);

        var response = new ResourceListResponse();
        response.Resources.AddRange(downstream.Select(MapToResourceResponse));
        response.TotalCount = downstream.Count();

        return response;
    }

    public override async Task<CheckCycleResponse> CheckCycle(CheckCycleRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SourceId, out var sourceId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid source ID format"));

        if (!Guid.TryParse(request.TargetId, out var targetId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid target ID format"));

        if (!Enum.TryParse<RelationshipType>(request.RelationshipType, true, out var relationshipType))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid relationship type"));

        var wouldCreateCycle = await _catalogService.WouldCreateCycleAsync(sourceId, targetId, relationshipType, context.CancellationToken);

        return new CheckCycleResponse
        {
            WouldCreateCycle = wouldCreateCycle
        };
    }

    public override async Task<FacetsResponse> GetFacets(GetFacetsRequest request, ServerCallContext context)
    {
        var facets = await _catalogService.GetFacetsAsync(
            string.IsNullOrEmpty(request.Query) ? null : request.Query,
            context.CancellationToken);

        var response = new FacetsResponse();
        foreach (var facet in facets)
        {
            response.Facets.Add(facet.Key, facet.Value);
        }

        return response;
    }

    // Mapping helpers

    private static ResourceResponse MapToResourceResponse(CatalogResource resource)
    {
        var response = new ResourceResponse
        {
            Id = resource.Id.ToString(),
            ResourceType = resource.ResourceType.ToString(),
            Name = resource.Name,
            Version = resource.Version,
            CreatedAt = resource.CreatedAt.ToString("O"),
            UpdatedAt = resource.UpdatedAt.ToString("O"),
            IsActive = resource.IsActive
        };

        if (!string.IsNullOrEmpty(resource.Namespace))
            response.Namespace = resource.Namespace;

        response.Metadata.Add(resource.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty));
        response.Tags.AddRange(resource.Tags);
        response.Properties.Add(resource.Properties);

        return response;
    }

    private static RelationshipResponse MapToRelationshipResponse(Relationship relationship)
    {
        var response = new RelationshipResponse
        {
            Id = relationship.Id.ToString(),
            SourceResourceId = relationship.SourceResourceId.ToString(),
            TargetResourceId = relationship.TargetResourceId.ToString(),
            RelationshipType = relationship.RelationshipType.ToString(),
            CreatedAt = relationship.CreatedAt.ToString("O"),
            IsBidirectional = relationship.IsBidirectional,
            Required = relationship.Required
        };

        if (!string.IsNullOrEmpty(relationship.CreatedBy))
            response.CreatedBy = relationship.CreatedBy;

        if (!string.IsNullOrEmpty(relationship.DependencyType))
            response.DependencyType = relationship.DependencyType;

        if (!string.IsNullOrEmpty(relationship.VersionConstraint))
            response.VersionConstraint = relationship.VersionConstraint;

        if (!string.IsNullOrEmpty(relationship.TransformationType))
            response.TransformationType = relationship.TransformationType;

        if (!string.IsNullOrEmpty(relationship.TransformationLogic))
            response.TransformationLogic = relationship.TransformationLogic;

        return response;
    }
}
