using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.Aggregations;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Persistence.Elasticsearch;

public class ElasticsearchSearchRepository : ISearchRepository
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ElasticsearchSearchRepository(ElasticsearchClient client, string indexName = "catalog-resources")
    {
        _client = client;
        _indexName = indexName;
    }

    public async Task<IEnumerable<CatalogResource>> SearchAsync(
        string query,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<CatalogResource>();

        if (pageSize <= 0 || pageSize > 1000)
            throw new ArgumentException("Page size must be between 1 and 1000", nameof(pageSize));

        if (pageNumber <= 0)
            throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));

        var from = (pageNumber - 1) * pageSize;

        var response = await _client.SearchAsync<SearchDocument>(s => s
            .Indices(_indexName)
            .From(from)
            .Size(pageSize)
            .Query(q => q
                .MultiMatch(m => m
                    .Query(query)
                    .Fields(new[] { "name^3", "description^2", "tags", "namespace", "properties.*" })
                    .Type(TextQueryType.BestFields)
                    .Fuzziness(new Fuzziness("AUTO"))
                )
            )
            .Sort(so => so
                .Score(new ScoreSort { Order = SortOrder.Desc })
            ), cancellationToken);

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch search failed: {response.ElasticsearchServerError?.Error?.Reason}");

        return response.Documents.Select(MapToCatalogResource);
    }

    public async Task<IEnumerable<CatalogResource>> AutocompleteAsync(
        string prefix,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchAsync<SearchDocument>(s => s
            .Indices(_indexName)
            .Size(limit)
            .Query(q => q
                .Bool(b => b
                    .Should(
                        sh => sh.Match(m => m
                            .Field("name.autocomplete")
                            .Query(prefix)
                        ),
                        sh => sh.Prefix(p => p
                            .Field("name.keyword")
                            .Value(prefix)
                            .Boost(2.0f)
                        )
                    )
                )
            )
            .Sort(so => so
                .Score(new ScoreSort { Order = SortOrder.Desc })
            ), cancellationToken);

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch autocomplete failed: {response.ElasticsearchServerError?.Error?.Reason}");

        return response.Documents.Select(MapToCatalogResource);
    }

    public async Task<IEnumerable<CatalogResource>> SearchByTypeAsync(
        ResourceType resourceType,
        string? query = null,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var from = (pageNumber - 1) * pageSize;

        SearchResponse<SearchDocument> response;

        if (string.IsNullOrEmpty(query))
        {
            response = await _client.SearchAsync<SearchDocument>(s => s
                .Indices(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(q => q
                    .Term(t => t
                        .Field("resource_type.keyword")
                        .Value(resourceType.ToString())
                    )
                ), cancellationToken);
        }
        else
        {
            response = await _client.SearchAsync<SearchDocument>(s => s
                .Indices(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            m => m.Term(t => t
                                .Field("resource_type.keyword")
                                .Value(resourceType.ToString())
                            ),
                            m => m.MultiMatch(mm => mm
                                .Query(query)
                                .Fields(new[] { "name^3", "description^2", "tags" })
                            )
                        )
                    )
                ), cancellationToken);
        }

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch search by type failed: {response.ElasticsearchServerError?.Error?.Reason}");

        return response.Documents.Select(MapToCatalogResource);
    }

    public async Task<IEnumerable<CatalogResource>> SearchByNamespaceAsync(
        string @namespace,
        string? query = null,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var from = (pageNumber - 1) * pageSize;

        SearchResponse<SearchDocument> response;

        if (string.IsNullOrEmpty(query))
        {
            response = await _client.SearchAsync<SearchDocument>(s => s
                .Indices(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(q => q
                    .Term(t => t
                        .Field("namespace.keyword")
                        .Value(@namespace)
                    )
                ), cancellationToken);
        }
        else
        {
            response = await _client.SearchAsync<SearchDocument>(s => s
                .Indices(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            m => m.Term(t => t
                                .Field("namespace.keyword")
                                .Value(@namespace)
                            ),
                            m => m.MultiMatch(mm => mm
                                .Query(query)
                                .Fields(new[] { "name^3", "description^2", "tags" })
                            )
                        )
                    )
                ), cancellationToken);
        }

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch search by namespace failed: {response.ElasticsearchServerError?.Error?.Reason}");

        return response.Documents.Select(MapToCatalogResource);
    }

    public async Task<IEnumerable<CatalogResource>> SearchByTagsAsync(
        IEnumerable<string> tags,
        bool matchAll = false,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var from = (pageNumber - 1) * pageSize;
        var tagList = tags.ToList();

        SearchResponse<SearchDocument> response;

        if (matchAll)
        {
            // For matchAll, we need all tags to be present
            var mustQueries = tagList.Select<string, Action<QueryDescriptor<SearchDocument>>>(
                tag => q => q.Term(t => t.Field("tags.keyword").Value(tag))
            ).ToArray();

            response = await _client.SearchAsync<SearchDocument>(s => s
                .Indices(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(q => q
                    .Bool(b => b
                        .Must(mustQueries)
                    )
                ), cancellationToken);
        }
        else
        {
            response = await _client.SearchAsync<SearchDocument>(s => s
                .Indices(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(q => q
                    .Terms(t => t
                        .Field("tags.keyword")
                        .Terms(new TermsQueryField(tagList.Select(FieldValue.String).ToArray()))
                    )
                ), cancellationToken);
        }

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch search by tags failed: {response.ElasticsearchServerError?.Error?.Reason}");

        return response.Documents.Select(MapToCatalogResource);
    }

    public async Task<Dictionary<string, long>> GetFacetsAsync(
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        SearchResponse<SearchDocument> response;

        if (string.IsNullOrEmpty(query))
        {
            response = await _client.SearchAsync<SearchDocument>(s => s
                .Indices(_indexName)
                .Size(0)
                .Query(q => q.MatchAll())
                .Aggregations(a => a
                    .Add("resource_types", agg => agg
                        .Terms(t => t
                            .Field("resource_type.keyword")
                            .Size(50)
                        )
                    )
                    .Add("namespaces", agg => agg
                        .Terms(t => t
                            .Field("namespace.keyword")
                            .Size(100)
                        )
                    )
                    .Add("tags", agg => agg
                        .Terms(t => t
                            .Field("tags.keyword")
                            .Size(100)
                        )
                    )
                ), cancellationToken);
        }
        else
        {
            response = await _client.SearchAsync<SearchDocument>(s => s
                .Indices(_indexName)
                .Size(0)
                .Query(q => q
                    .MultiMatch(mm => mm
                        .Query(query)
                        .Fields(new[] { "name", "description", "tags" })
                    )
                )
                .Aggregations(a => a
                    .Add("resource_types", agg => agg
                        .Terms(t => t
                            .Field("resource_type.keyword")
                            .Size(50)
                        )
                    )
                    .Add("namespaces", agg => agg
                        .Terms(t => t
                            .Field("namespace.keyword")
                            .Size(100)
                        )
                    )
                    .Add("tags", agg => agg
                        .Terms(t => t
                            .Field("tags.keyword")
                            .Size(100)
                        )
                    )
                ), cancellationToken);
        }

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch facets failed: {response.ElasticsearchServerError?.Error?.Reason}");

        var facets = new Dictionary<string, long>();

        if (response.Aggregations != null)
        {
            if (response.Aggregations.TryGetValue("resource_types", out var resourceTypesAgg))
            {
                var termsAgg = resourceTypesAgg as StringTermsAggregate;
                if (termsAgg?.Buckets != null)
                {
                    foreach (var bucket in termsAgg.Buckets)
                    {
                        if (bucket is StringTermsBucket stringBucket)
                        {
                            facets[$"type:{stringBucket.Key.Value}"] = stringBucket.DocCount;
                        }
                    }
                }
            }

            if (response.Aggregations.TryGetValue("namespaces", out var namespacesAgg))
            {
                var termsAgg = namespacesAgg as StringTermsAggregate;
                if (termsAgg?.Buckets != null)
                {
                    foreach (var bucket in termsAgg.Buckets)
                    {
                        if (bucket is StringTermsBucket stringBucket)
                        {
                            facets[$"namespace:{stringBucket.Key.Value}"] = stringBucket.DocCount;
                        }
                    }
                }
            }

            if (response.Aggregations.TryGetValue("tags", out var tagsAgg))
            {
                var termsAgg = tagsAgg as StringTermsAggregate;
                if (termsAgg?.Buckets != null)
                {
                    foreach (var bucket in termsAgg.Buckets)
                    {
                        if (bucket is StringTermsBucket stringBucket)
                        {
                            facets[$"tag:{stringBucket.Key.Value}"] = stringBucket.DocCount;
                        }
                    }
                }
            }
        }

        return facets;
    }

    public async Task IndexAsync(CatalogResource resource, CancellationToken cancellationToken = default)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        if (resource.Id == Guid.Empty)
            throw new ArgumentException("Resource ID cannot be empty", nameof(resource));

        if (string.IsNullOrWhiteSpace(resource.Name))
            throw new ArgumentException("Resource name cannot be null or empty", nameof(resource));

        var document = MapToSearchDocument(resource);

        var response = await _client.IndexAsync(
            document,
            idx => idx.Index(_indexName).Id(resource.Id.ToString()),
            cancellationToken);

        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch indexing failed: {response.ElasticsearchServerError?.Error?.Reason}");
    }

    public async Task BulkIndexAsync(IEnumerable<CatalogResource> resources, CancellationToken cancellationToken = default)
    {
        if (resources == null)
            throw new ArgumentNullException(nameof(resources));

        var resourceList = resources.ToList();
        if (!resourceList.Any())
            return;

        var documents = resourceList.Select(MapToSearchDocument).ToList();

        var response = await _client.BulkAsync(b => b
            .Index(_indexName)
            .IndexMany(documents, (descriptor, doc) => descriptor
                .Id(doc.Id)
            ), cancellationToken);

        if (!response.IsValidResponse || response.Errors)
        {
            var errors = string.Join(", ", response.ItemsWithErrors.Select(i => i.Error?.Reason));
            throw new InvalidOperationException($"Elasticsearch bulk indexing failed: {errors}");
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Resource ID cannot be empty", nameof(id));

        var response = await _client.DeleteAsync(
            _indexName,
            id.ToString(),
            cancellationToken);

        if (!response.IsValidResponse && response.Result != Result.NotFound)
            throw new InvalidOperationException($"Elasticsearch delete failed: {response.ElasticsearchServerError?.Error?.Reason}");
    }

    public async Task ReindexAllAsync(IEnumerable<CatalogResource> resources, CancellationToken cancellationToken = default)
    {
        // Delete existing index
        var deleteResponse = await _client.Indices.DeleteAsync(_indexName, cancellationToken);

        if (!deleteResponse.IsValidResponse && deleteResponse.ElasticsearchServerError?.Status != 404)
            throw new InvalidOperationException($"Elasticsearch index deletion failed: {deleteResponse.ElasticsearchServerError?.Error?.Reason}");

        // Small delay to ensure deletion propagates through cluster
        // In production, consider using refresh=wait_for parameter instead
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

        // Recreate index with mappings
        await CreateIndexIfNotExistsAsync(cancellationToken);

        // Bulk index all resources
        await BulkIndexAsync(resources, cancellationToken);
    }

    private async Task CreateIndexIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        var existsResponse = await _client.Indices.ExistsAsync(_indexName, cancellationToken);

        if (existsResponse.Exists)
            return;

        // Index will be created automatically or by external script
        // This method is for future enhancement
    }

    private static SearchDocument MapToSearchDocument(CatalogResource resource)
    {
        return new SearchDocument
        {
            Id = resource.Id.ToString(),
            ResourceType = resource.ResourceType.ToString(),
            Name = resource.Name,
            Namespace = resource.Namespace,
            Description = resource.Metadata.ContainsKey("description")
                ? resource.Metadata["description"]?.ToString()
                : null,
            Tags = resource.Tags,
            Version = resource.Version,
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt,
            IsActive = resource.IsActive,
            Properties = resource.Properties.ToDictionary(p => p.Key, p => (object)p.Value),
            Metadata = resource.Metadata
        };
    }

    private static CatalogResource MapToCatalogResource(SearchDocument document)
    {
        // Safely parse Guid and ResourceType with fallbacks
        if (!Guid.TryParse(document.Id, out var id))
        {
            id = Guid.Empty;
        }

        if (!Enum.TryParse<ResourceType>(document.ResourceType, ignoreCase: true, out var resourceType))
        {
            resourceType = ResourceType.Unknown;
        }

        return new CatalogResource
        {
            Id = id,
            ResourceType = resourceType,
            Name = document.Name,
            Namespace = document.Namespace,
            Version = document.Version,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            IsActive = document.IsActive,
            Tags = document.Tags ?? new List<string>(),
            Metadata = document.Metadata ?? new Dictionary<string, object>(),
            Properties = document.Properties?.ToDictionary(p => p.Key, p => p.Value?.ToString() ?? string.Empty)
                ?? new Dictionary<string, string>()
        };
    }
}

public class SearchDocument
{
    public string Id { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Version { get; set; } = "1.0.0";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object>? Properties { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
