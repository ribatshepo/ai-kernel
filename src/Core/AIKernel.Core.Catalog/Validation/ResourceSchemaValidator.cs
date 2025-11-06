using System.Text.Json;
using System.Text.RegularExpressions;
using AIKernel.Core.Catalog.Contracts.Models;
using AIKernel.Core.Catalog.Contracts.Enums;

namespace AIKernel.Core.Catalog.Validation;

/// <summary>
/// Validates catalog resources against schema rules and business constraints
/// </summary>
public class ResourceSchemaValidator
{
    private static readonly Regex NamePattern = new(@"^[a-zA-Z0-9][a-zA-Z0-9\-_.]{0,62}[a-zA-Z0-9]$", RegexOptions.Compiled);
    private static readonly Regex NamespacePattern = new(@"^[a-z0-9][a-z0-9\-]{0,62}[a-z0-9]$", RegexOptions.Compiled);
    private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9\-]+)?$", RegexOptions.Compiled);

    private readonly Dictionary<ResourceType, ResourceSchema> _schemas = new()
    {
        [ResourceType.Service] = new ResourceSchema
        {
            RequiredProperties = new[] { "endpoint", "protocol" },
            OptionalProperties = new[] { "port", "health_check_path", "timeout_seconds" },
            RequiredMetadata = new[] { "description" }
        },
        [ResourceType.Database] = new ResourceSchema
        {
            RequiredProperties = new[] { "connection_string", "provider" },
            OptionalProperties = new[] { "max_connections", "timeout_seconds", "ssl_mode" },
            RequiredMetadata = new[] { "description", "environment" }
        },
        [ResourceType.Table] = new ResourceSchema
        {
            RequiredProperties = new[] { "database_id", "schema_name", "table_name" },
            OptionalProperties = new[] { "row_count", "size_bytes" },
            RequiredMetadata = new[] { "description" }
        },
        [ResourceType.Model] = new ResourceSchema
        {
            RequiredProperties = new[] { "model_type", "framework" },
            OptionalProperties = new[] { "input_shape", "output_shape", "parameters_count" },
            RequiredMetadata = new[] { "description", "training_dataset" }
        },
        [ResourceType.Dataset] = new ResourceSchema
        {
            RequiredProperties = new[] { "storage_location", "format" },
            OptionalProperties = new[] { "row_count", "column_count", "size_bytes" },
            RequiredMetadata = new[] { "description", "schema" }
        },
        [ResourceType.API] = new ResourceSchema
        {
            RequiredProperties = new[] { "base_url", "version" },
            OptionalProperties = new[] { "auth_type", "rate_limit" },
            RequiredMetadata = new[] { "description", "documentation_url" }
        }
    };

    /// <summary>
    /// Validates a catalog resource against schema rules
    /// </summary>
    public ValidationResult Validate(CatalogResource resource)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate basic fields
        if (string.IsNullOrWhiteSpace(resource.Name))
            errors.Add("Resource name is required");
        else if (!NamePattern.IsMatch(resource.Name))
            errors.Add("Resource name must start and end with alphanumeric characters and contain only alphanumeric, hyphen, underscore, or dot");

        if (!string.IsNullOrEmpty(resource.Namespace) && !NamespacePattern.IsMatch(resource.Namespace))
            errors.Add("Namespace must be lowercase and contain only alphanumeric or hyphen characters");

        if (!VersionPattern.IsMatch(resource.Version))
            errors.Add("Version must follow semantic versioning (e.g., 1.0.0 or 1.0.0-beta)");

        // Validate resource type
        if (resource.ResourceType == ResourceType.Unknown)
            errors.Add("Resource type must be specified");

        // Validate against resource-specific schema
        if (_schemas.TryGetValue(resource.ResourceType, out var schema))
        {
            ValidateProperties(resource, schema, errors, warnings);
            ValidateMetadata(resource, schema, errors, warnings);
        }

        // Validate tags
        if (resource.Tags.Any(t => string.IsNullOrWhiteSpace(t)))
            errors.Add("Tags cannot be empty or whitespace");

        if (resource.Tags.Distinct().Count() != resource.Tags.Count)
            warnings.Add("Duplicate tags detected");

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private void ValidateProperties(CatalogResource resource, ResourceSchema schema, List<string> errors, List<string> warnings)
    {
        // Check required properties
        foreach (var requiredProp in schema.RequiredProperties)
        {
            if (!resource.Properties.ContainsKey(requiredProp))
                errors.Add($"Required property '{requiredProp}' is missing for {resource.ResourceType}");
            else if (string.IsNullOrWhiteSpace(resource.Properties[requiredProp]))
                errors.Add($"Required property '{requiredProp}' cannot be empty");
        }

        // Warn about unexpected properties
        var allowedProps = schema.RequiredProperties.Concat(schema.OptionalProperties).ToHashSet();
        foreach (var prop in resource.Properties.Keys)
        {
            if (!allowedProps.Contains(prop))
                warnings.Add($"Unexpected property '{prop}' for {resource.ResourceType}");
        }
    }

    private void ValidateMetadata(CatalogResource resource, ResourceSchema schema, List<string> errors, List<string> warnings)
    {
        // Check required metadata
        foreach (var requiredMeta in schema.RequiredMetadata)
        {
            if (!resource.Metadata.ContainsKey(requiredMeta))
                errors.Add($"Required metadata '{requiredMeta}' is missing for {resource.ResourceType}");
            else if (resource.Metadata[requiredMeta] == null ||
                     string.IsNullOrWhiteSpace(resource.Metadata[requiredMeta]?.ToString()))
                errors.Add($"Required metadata '{requiredMeta}' cannot be empty");
        }

        // Validate metadata values are serializable
        foreach (var (key, value) in resource.Metadata)
        {
            try
            {
                JsonSerializer.Serialize(value);
            }
            catch
            {
                errors.Add($"Metadata '{key}' value is not JSON serializable");
            }
        }
    }

    /// <summary>
    /// Validates that a resource can be updated (immutable fields check)
    /// </summary>
    public ValidationResult ValidateUpdate(CatalogResource existing, CatalogResource updated)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Immutable fields
        if (existing.Id != updated.Id)
            errors.Add("Resource ID cannot be changed");

        if (existing.ResourceType != updated.ResourceType)
            errors.Add("Resource type cannot be changed");

        if (existing.CreatedAt != updated.CreatedAt)
            warnings.Add("CreatedAt timestamp should not be modified");

        if (existing.CreatedBy != updated.CreatedBy)
            warnings.Add("CreatedBy should not be modified");

        // Validate the updated resource itself
        var schemaValidation = Validate(updated);
        errors.AddRange(schemaValidation.Errors);
        warnings.AddRange(schemaValidation.Warnings);

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
}

public class ResourceSchema
{
    public string[] RequiredProperties { get; set; } = Array.Empty<string>();
    public string[] OptionalProperties { get; set; } = Array.Empty<string>();
    public string[] RequiredMetadata { get; set; } = Array.Empty<string>();
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public string GetErrorMessage() => string.Join("; ", Errors);
    public string GetWarningMessage() => string.Join("; ", Warnings);
}
