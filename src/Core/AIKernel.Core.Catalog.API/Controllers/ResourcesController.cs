using Microsoft.AspNetCore.Mvc;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ResourcesController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(ICatalogService catalogService, ILogger<ResourcesController> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a resource by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CatalogResource), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogResource>> GetResource(Guid id, CancellationToken cancellationToken)
    {
        var resource = await _catalogService.GetResourceByIdAsync(id, cancellationToken);

        if (resource == null)
            return NotFound(new { message = $"Resource {id} not found" });

        return Ok(resource);
    }

    /// <summary>
    /// Gets a resource by name and namespace
    /// </summary>
    [HttpGet("by-name")]
    [ProducesResponseType(typeof(CatalogResource), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogResource>> GetResourceByName(
        [FromQuery] string name,
        [FromQuery] string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        var resource = await _catalogService.GetResourceByNameAsync(name, @namespace, cancellationToken);

        if (resource == null)
            return NotFound(new { message = $"Resource '{name}' not found in namespace '{@namespace ?? "default"}'" });

        return Ok(resource);
    }

    /// <summary>
    /// Searches resources using full-text search
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> SearchResources(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Search query is required" });

        var resources = await _catalogService.SearchResourcesAsync(query, cancellationToken);
        return Ok(resources);
    }

    /// <summary>
    /// Gets autocomplete suggestions for resource names
    /// </summary>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> AutocompleteResources(
        [FromQuery] string prefix,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return BadRequest(new { message = "Prefix is required" });

        var resources = await _catalogService.AutocompleteAsync(prefix, limit, cancellationToken);
        return Ok(resources);
    }

    /// <summary>
    /// Gets resources by type
    /// </summary>
    [HttpGet("by-type/{resourceType}")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> GetResourcesByType(
        string resourceType,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ResourceType>(resourceType, true, out var type))
            return BadRequest(new { message = $"Invalid resource type: {resourceType}" });

        var resources = await _catalogService.GetResourcesByTypeAsync(type, cancellationToken);
        return Ok(resources);
    }

    /// <summary>
    /// Gets resources by namespace
    /// </summary>
    [HttpGet("by-namespace/{namespace}")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> GetResourcesByNamespace(
        string @namespace,
        CancellationToken cancellationToken)
    {
        var resources = await _catalogService.GetResourcesByNamespaceAsync(@namespace, cancellationToken);
        return Ok(resources);
    }

    /// <summary>
    /// Gets resources by tags
    /// </summary>
    [HttpGet("by-tags")]
    [ProducesResponseType(typeof(IEnumerable<CatalogResource>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CatalogResource>>> GetResourcesByTags(
        [FromQuery] string[] tags,
        CancellationToken cancellationToken)
    {
        if (tags == null || tags.Length == 0)
            return BadRequest(new { message = "At least one tag is required" });

        var resources = await _catalogService.GetResourcesByTagsAsync(tags, cancellationToken);
        return Ok(resources);
    }

    /// <summary>
    /// Registers a new resource
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CatalogResource), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CatalogResource>> RegisterResource(
        [FromBody] CatalogResource resource,
        CancellationToken cancellationToken)
    {
        if (resource == null)
            return BadRequest(new { message = "Resource data is required" });

        if (string.IsNullOrWhiteSpace(resource.Name))
            return BadRequest(new { message = "Resource name is required" });

        try
        {
            var createdResource = await _catalogService.RegisterResourceAsync(resource, cancellationToken);
            return CreatedAtAction(nameof(GetResource), new { id = createdResource.Id }, createdResource);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to register resource: {Name}", resource.Name);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing resource
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CatalogResource), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CatalogResource>> UpdateResource(
        Guid id,
        [FromBody] CatalogResource resource,
        CancellationToken cancellationToken)
    {
        if (resource == null)
            return BadRequest(new { message = "Resource data is required" });

        if (id != resource.Id)
            return BadRequest(new { message = "Resource ID in URL must match ID in body" });

        try
        {
            var updatedResource = await _catalogService.UpdateResourceAsync(resource, cancellationToken);
            return Ok(updatedResource);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update resource: {Id}", id);
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a resource
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteResource(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteResourceAsync(id, cancellationToken);

        if (!deleted)
            return NotFound(new { message = $"Resource {id} not found" });

        return NoContent();
    }

    /// <summary>
    /// Gets facets for filtering resources
    /// </summary>
    [HttpGet("facets")]
    [ProducesResponseType(typeof(Dictionary<string, long>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, long>>> GetFacets(
        [FromQuery] string? query = null,
        CancellationToken cancellationToken = default)
    {
        var facets = await _catalogService.GetFacetsAsync(query, cancellationToken);
        return Ok(facets);
    }
}
