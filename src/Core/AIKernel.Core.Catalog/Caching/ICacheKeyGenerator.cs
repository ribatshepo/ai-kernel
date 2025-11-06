namespace AIKernel.Core.Catalog.Caching;

public interface ICacheKeyGenerator
{
    string GenerateResourceKey(Guid resourceId);
    string GenerateResourceByNameKey(string name);
    string GenerateResourceByTypeKey(string resourceType, int pageNumber, int pageSize);
    string GenerateResourceByNamespaceKey(string namespaceName, int pageNumber, int pageSize);
    string GenerateResourceByTagsKey(IEnumerable<string> tags, int pageNumber, int pageSize);
    string GenerateSearchKey(string query, int pageNumber, int pageSize);
    string GenerateAutocompleteKey(string prefix, int limit);
    string GenerateRelationshipKey(Guid relationshipId);
    string GenerateDependenciesKey(Guid resourceId, int maxDepth);
    string GenerateDependentsKey(Guid resourceId, int maxDepth);
    string GenerateLineageKey(Guid resourceId, string direction, int maxDepth);
    string GenerateVersionHistoryKey(Guid resourceId, int pageNumber, int pageSize);
    string GenerateFacetsKey(string facetType);
    string GenerateInvalidationPattern(string prefix, params string[] parameters);
}
