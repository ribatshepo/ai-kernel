using Microsoft.AspNetCore.Mvc;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class RelationshipsController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<RelationshipsController> _logger;

    public RelationshipsController(ICatalogService catalogService, ILogger<RelationshipsController> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a relationship by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Relationship), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Relationship>> GetRelationship(Guid id, CancellationToken cancellationToken)
    {
        var relationship = await _catalogService.GetRelationshipByIdAsync(id, cancellationToken);

        if (relationship == null)
            return NotFound(new { message = $"Relationship {id} not found" });

        return Ok(relationship);
    }

    /// <summary>
    /// Gets all relationships from a source resource
    /// </summary>
    [HttpGet("by-source/{sourceId}")]
    [ProducesResponseType(typeof(IEnumerable<Relationship>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Relationship>>> GetRelationshipsBySource(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var relationships = await _catalogService.GetRelationshipsBySourceAsync(sourceId, cancellationToken);
        return Ok(relationships);
    }

    /// <summary>
    /// Gets all relationships to a target resource
    /// </summary>
    [HttpGet("by-target/{targetId}")]
    [ProducesResponseType(typeof(IEnumerable<Relationship>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Relationship>>> GetRelationshipsByTarget(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var relationships = await _catalogService.GetRelationshipsByTargetAsync(targetId, cancellationToken);
        return Ok(relationships);
    }

    /// <summary>
    /// Gets all relationships of a specific type
    /// </summary>
    [HttpGet("by-type/{relationshipType}")]
    [ProducesResponseType(typeof(IEnumerable<Relationship>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<Relationship>>> GetRelationshipsByType(
        string relationshipType,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RelationshipType>(relationshipType, true, out var type))
            return BadRequest(new { message = $"Invalid relationship type: {relationshipType}" });

        var relationships = await _catalogService.GetRelationshipsByTypeAsync(type, cancellationToken);
        return Ok(relationships);
    }

    /// <summary>
    /// Gets all relationships between two resources
    /// </summary>
    [HttpGet("between")]
    [ProducesResponseType(typeof(IEnumerable<Relationship>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Relationship>>> GetRelationshipsBetween(
        [FromQuery] Guid sourceId,
        [FromQuery] Guid targetId,
        CancellationToken cancellationToken)
    {
        var relationships = await _catalogService.GetRelationshipsBetweenAsync(sourceId, targetId, cancellationToken);
        return Ok(relationships);
    }

    /// <summary>
    /// Creates a new relationship between resources
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Relationship), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Relationship>> CreateRelationship(
        [FromBody] Relationship relationship,
        CancellationToken cancellationToken)
    {
        if (relationship == null)
            return BadRequest(new { message = "Relationship data is required" });

        if (relationship.SourceResourceId == Guid.Empty)
            return BadRequest(new { message = "Source resource ID is required" });

        if (relationship.TargetResourceId == Guid.Empty)
            return BadRequest(new { message = "Target resource ID is required" });

        if (relationship.SourceResourceId == relationship.TargetResourceId)
            return BadRequest(new { message = "Source and target resources cannot be the same" });

        try
        {
            var createdRelationship = await _catalogService.CreateRelationshipAsync(relationship, cancellationToken);
            return CreatedAtAction(nameof(GetRelationship), new { id = createdRelationship.Id }, createdRelationship);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create relationship from {Source} to {Target}",
                relationship.SourceResourceId, relationship.TargetResourceId);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a relationship
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRelationship(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteRelationshipAsync(id, cancellationToken);

        if (!deleted)
            return NotFound(new { message = $"Relationship {id} not found" });

        return NoContent();
    }

    /// <summary>
    /// Checks if creating a relationship would introduce a cycle
    /// </summary>
    [HttpPost("check-cycle")]
    [ProducesResponseType(typeof(CheckCycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CheckCycleResponse>> CheckCycle(
        [FromBody] CheckCycleRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "Request data is required" });

        if (!Enum.TryParse<RelationshipType>(request.RelationshipType, true, out var type))
            return BadRequest(new { message = $"Invalid relationship type: {request.RelationshipType}" });

        var wouldCreateCycle = await _catalogService.WouldCreateCycleAsync(
            request.SourceId,
            request.TargetId,
            type,
            cancellationToken);

        return Ok(new CheckCycleResponse(wouldCreateCycle));
    }

    /// <summary>
    /// Gets resources that a resource depends on
    /// </summary>
    [HttpGet("{resourceId}/dependencies")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> GetDependencies(
        Guid resourceId,
        [FromQuery] int depth = 1,
        CancellationToken cancellationToken = default)
    {
        if (depth < 1 || depth > 10)
            return BadRequest(new { message = "Depth must be between 1 and 10" });

        var dependencies = await _catalogService.GetDependenciesAsync(resourceId, depth, cancellationToken);
        return Ok(dependencies);
    }

    /// <summary>
    /// Gets resources that depend on this resource
    /// </summary>
    [HttpGet("{resourceId}/dependents")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> GetDependents(
        Guid resourceId,
        [FromQuery] int depth = 1,
        CancellationToken cancellationToken = default)
    {
        if (depth < 1 || depth > 10)
            return BadRequest(new { message = "Depth must be between 1 and 10" });

        var dependents = await _catalogService.GetDependentsAsync(resourceId, depth, cancellationToken);
        return Ok(dependents);
    }

    /// <summary>
    /// Gets upstream data lineage
    /// </summary>
    [HttpGet("{resourceId}/lineage/upstream")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> GetLineageUpstream(
        Guid resourceId,
        [FromQuery] int depth = 10,
        CancellationToken cancellationToken = default)
    {
        if (depth < 1 || depth > 50)
            return BadRequest(new { message = "Depth must be between 1 and 50" });

        var upstream = await _catalogService.GetLineageUpstreamAsync(resourceId, depth, cancellationToken);
        return Ok(upstream);
    }

    /// <summary>
    /// Gets downstream data lineage
    /// </summary>
    [HttpGet("{resourceId}/lineage/downstream")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> GetLineageDownstream(
        Guid resourceId,
        [FromQuery] int depth = 10,
        CancellationToken cancellationToken = default)
    {
        if (depth < 1 || depth > 50)
            return BadRequest(new { message = "Depth must be between 1 and 50" });

        var downstream = await _catalogService.GetLineageDownstreamAsync(resourceId, depth, cancellationToken);
        return Ok(downstream);
    }
}

public record CheckCycleRequest(Guid SourceId, Guid TargetId, string RelationshipType);
public record CheckCycleResponse(bool WouldCreateCycle);
