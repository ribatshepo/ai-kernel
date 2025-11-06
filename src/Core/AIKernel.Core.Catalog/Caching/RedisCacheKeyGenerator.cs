using System.Security.Cryptography;
using System.Text;

namespace AIKernel.Core.Catalog.Caching;

public class RedisCacheKeyGenerator : ICacheKeyGenerator
{
    private readonly string _instanceName;
    private readonly string _version;

    public RedisCacheKeyGenerator(string instanceName, string version = CacheConstants.ApiVersion)
    {
        _instanceName = instanceName ?? throw new ArgumentNullException(nameof(instanceName));
        _version = version ?? throw new ArgumentNullException(nameof(version));
    }

    public string GenerateResourceKey(Guid resourceId)
    {
        return BuildKey(CacheConstants.Prefixes.Resource, CacheConstants.Operations.GetById, resourceId.ToString());
    }

    public string GenerateResourceByNameKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Resource name cannot be null or whitespace.", nameof(name));

        return BuildKey(CacheConstants.Prefixes.Resource, CacheConstants.Operations.GetByName, NormalizeName(name));
    }

    public string GenerateResourceByTypeKey(string resourceType, int pageNumber, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type cannot be null or whitespace.", nameof(resourceType));

        ValidatePagination(pageNumber, pageSize);

        return BuildKey(
            CacheConstants.Prefixes.Resource,
            CacheConstants.Operations.GetByType,
            resourceType,
            $"page{CacheConstants.Separators.Parameter}{pageNumber}",
            $"size{CacheConstants.Separators.Parameter}{pageSize}"
        );
    }

    public string GenerateResourceByNamespaceKey(string namespaceName, int pageNumber, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            throw new ArgumentException("Namespace name cannot be null or whitespace.", nameof(namespaceName));

        ValidatePagination(pageNumber, pageSize);

        return BuildKey(
            CacheConstants.Prefixes.Resource,
            CacheConstants.Operations.GetByNamespace,
            NormalizeName(namespaceName),
            $"page{CacheConstants.Separators.Parameter}{pageNumber}",
            $"size{CacheConstants.Separators.Parameter}{pageSize}"
        );
    }

    public string GenerateResourceByTagsKey(IEnumerable<string> tags, int pageNumber, int pageSize)
    {
        if (tags == null || !tags.Any())
            throw new ArgumentException("Tags collection cannot be null or empty.", nameof(tags));

        ValidatePagination(pageNumber, pageSize);

        var sortedTags = tags.OrderBy(t => t).Select(NormalizeName);
        var tagsHash = ComputeHash(string.Join(",", sortedTags));

        return BuildKey(
            CacheConstants.Prefixes.Resource,
            CacheConstants.Operations.GetByTags,
            tagsHash,
            $"page{CacheConstants.Separators.Parameter}{pageNumber}",
            $"size{CacheConstants.Separators.Parameter}{pageSize}"
        );
    }

    public string GenerateSearchKey(string query, int pageNumber, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Search query cannot be null or whitespace.", nameof(query));

        ValidatePagination(pageNumber, pageSize);

        var queryHash = ComputeHash(query.Trim().ToLowerInvariant());

        return BuildKey(
            CacheConstants.Prefixes.Search,
            CacheConstants.Operations.Search,
            queryHash,
            $"page{CacheConstants.Separators.Parameter}{pageNumber}",
            $"size{CacheConstants.Separators.Parameter}{pageSize}"
        );
    }

    public string GenerateAutocompleteKey(string prefix, int limit)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Autocomplete prefix cannot be null or whitespace.", nameof(prefix));

        if (limit <= 0)
            throw new ArgumentException("Limit must be greater than zero.", nameof(limit));

        return BuildKey(
            CacheConstants.Prefixes.Search,
            CacheConstants.Operations.Autocomplete,
            NormalizeName(prefix),
            $"limit{CacheConstants.Separators.Parameter}{limit}"
        );
    }

    public string GenerateRelationshipKey(Guid relationshipId)
    {
        return BuildKey(CacheConstants.Prefixes.Relationship, CacheConstants.Operations.GetById, relationshipId.ToString());
    }

    public string GenerateDependenciesKey(Guid resourceId, int maxDepth)
    {
        ValidateDepth(maxDepth);

        return BuildKey(
            CacheConstants.Prefixes.Lineage,
            "dependencies",
            resourceId.ToString(),
            $"depth{CacheConstants.Separators.Parameter}{maxDepth}"
        );
    }

    public string GenerateDependentsKey(Guid resourceId, int maxDepth)
    {
        ValidateDepth(maxDepth);

        return BuildKey(
            CacheConstants.Prefixes.Lineage,
            "dependents",
            resourceId.ToString(),
            $"depth{CacheConstants.Separators.Parameter}{maxDepth}"
        );
    }

    public string GenerateLineageKey(Guid resourceId, string direction, int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(direction))
            throw new ArgumentException("Direction cannot be null or whitespace.", nameof(direction));

        ValidateDepth(maxDepth);

        return BuildKey(
            CacheConstants.Prefixes.Lineage,
            CacheConstants.Operations.GetLineage,
            resourceId.ToString(),
            direction.ToLowerInvariant(),
            $"depth{CacheConstants.Separators.Parameter}{maxDepth}"
        );
    }

    public string GenerateVersionHistoryKey(Guid resourceId, int pageNumber, int pageSize)
    {
        ValidatePagination(pageNumber, pageSize);

        return BuildKey(
            CacheConstants.Prefixes.Version,
            CacheConstants.Operations.GetVersionHistory,
            resourceId.ToString(),
            $"page{CacheConstants.Separators.Parameter}{pageNumber}",
            $"size{CacheConstants.Separators.Parameter}{pageSize}"
        );
    }

    public string GenerateFacetsKey(string facetType)
    {
        if (string.IsNullOrWhiteSpace(facetType))
            throw new ArgumentException("Facet type cannot be null or whitespace.", nameof(facetType));

        return BuildKey(CacheConstants.Prefixes.Facets, facetType.ToLowerInvariant());
    }

    public string GenerateInvalidationPattern(string prefix, params string[] parameters)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be null or whitespace.", nameof(prefix));

        var parts = new List<string>
        {
            _instanceName,
            _version,
            prefix
        };

        if (parameters != null && parameters.Length > 0)
        {
            parts.AddRange(parameters);
        }

        return string.Join(CacheConstants.Separators.Namespace, parts) + "*";
    }

    private string BuildKey(params string[] parts)
    {
        if (parts == null || parts.Length == 0)
            throw new ArgumentException("Key parts cannot be null or empty.", nameof(parts));

        var keyParts = new List<string>
        {
            _instanceName,
            _version
        };

        keyParts.AddRange(parts);

        return string.Join(CacheConstants.Separators.Namespace, keyParts);
    }

    private static string NormalizeName(string name)
    {
        return name.Trim().ToLowerInvariant();
    }

    private static string ComputeHash(string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant()[..16];
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 0)
            throw new ArgumentException("Page number cannot be negative.", nameof(pageNumber));

        if (pageSize <= 0)
            throw new ArgumentException("Page size must be greater than zero.", nameof(pageSize));
    }

    private static void ValidateDepth(int maxDepth)
    {
        if (maxDepth <= 0)
            throw new ArgumentException("Max depth must be greater than zero.", nameof(maxDepth));
    }
}
