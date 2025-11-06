namespace AIKernel.Core.Catalog.Caching;

public static class CacheConstants
{
    public const string ApiVersion = "v1";

    public static class Prefixes
    {
        public const string Resource = "resource";
        public const string Relationship = "relationship";
        public const string Search = "search";
        public const string Version = "version";
        public const string Lineage = "lineage";
        public const string Facets = "facets";
        public const string Session = "session";
    }

    public static class Separators
    {
        public const string Namespace = ":";
        public const string Parameter = "|";
        public const string Version = "@";
    }

    public static class Tags
    {
        public const string ResourceType = "type";
        public const string Namespace = "ns";
        public const string Operation = "op";
        public const string HitMiss = "hit_miss";
    }

    public static class Operations
    {
        public const string GetById = "get_by_id";
        public const string GetByName = "get_by_name";
        public const string GetByType = "get_by_type";
        public const string GetByNamespace = "get_by_namespace";
        public const string GetByTags = "get_by_tags";
        public const string Search = "search";
        public const string Autocomplete = "autocomplete";
        public const string GetLineage = "get_lineage";
        public const string GetVersionHistory = "get_version_history";
    }
}
