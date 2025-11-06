using AIKernel.Core.EventBus.Configuration;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIKernel.Core.EventBus.SchemaRegistry;

/// <summary>
/// Confluent Schema Registry implementation.
/// </summary>
public class ConfluentSchemaRegistry : ISchemaRegistry, IDisposable
{
    private readonly ISchemaRegistryClient _client;
    private readonly ILogger<ConfluentSchemaRegistry> _logger;
    private readonly SchemaRegistryConfiguration _configuration;

    public ConfluentSchemaRegistry(
        IOptions<EventBusConfiguration> configuration,
        ILogger<ConfluentSchemaRegistry> logger)
    {
        _configuration = configuration.Value.SchemaRegistry;
        _logger = logger;

        var config = new SchemaRegistryConfig
        {
            Url = _configuration.Url,
            BasicAuthUserInfo = _configuration.BasicAuthUserInfo,
            RequestTimeoutMs = _configuration.RequestTimeoutMs,
            MaxCachedSchemas = _configuration.MaxCachedSchemas
        };

        _client = new CachedSchemaRegistryClient(config);

        _logger.LogInformation(
            "Schema registry client initialized. URL: {Url}, MaxCachedSchemas: {MaxCachedSchemas}",
            _configuration.Url,
            _configuration.MaxCachedSchemas);
    }

    public async Task<int> RegisterSchemaAsync(
        string subject,
        string schema,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var schemaObj = new Confluent.SchemaRegistry.Schema(schema, SchemaType.Avro);
            var schemaId = await _client.RegisterSchemaAsync(subject, schemaObj, normalize: true);

            _logger.LogDebug(
                "Schema registered successfully. Subject: {Subject}, SchemaId: {SchemaId}",
                subject,
                schemaId);

            return schemaId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register schema for subject {Subject}", subject);
            throw new SchemaRegistryException($"Failed to register schema for subject '{subject}'", ex);
        }
    }

    public async Task<string> GetSchemaAsync(
        int schemaId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var schema = await _client.GetSchemaAsync(schemaId);

            _logger.LogDebug("Schema retrieved successfully. SchemaId: {SchemaId}", schemaId);

            return schema.SchemaString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schema with ID {SchemaId}", schemaId);
            throw new SchemaRegistryException($"Failed to get schema with ID {schemaId}", ex);
        }
    }

    public async Task<(int Id, string Schema)> GetLatestSchemaAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latestSchema = await _client.GetLatestSchemaAsync(subject);

            _logger.LogDebug(
                "Latest schema retrieved successfully. Subject: {Subject}, SchemaId: {SchemaId}, Version: {Version}",
                subject,
                latestSchema.Id,
                latestSchema.Version);

            return (latestSchema.Id, latestSchema.SchemaString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest schema for subject {Subject}", subject);
            throw new SchemaRegistryException($"Failed to get latest schema for subject '{subject}'", ex);
        }
    }

    public async Task<IEnumerable<int>> GetSchemaVersionsAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await _client.GetSubjectVersionsAsync(subject);

            _logger.LogDebug(
                "Schema versions retrieved successfully. Subject: {Subject}, VersionCount: {Count}",
                subject,
                versions?.Count ?? 0);

            return versions ?? new List<int>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schema versions for subject {Subject}", subject);
            throw new SchemaRegistryException($"Failed to get schema versions for subject '{subject}'", ex);
        }
    }

    public async Task<bool> CheckCompatibilityAsync(
        string subject,
        string schema,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var schemaObj = new Confluent.SchemaRegistry.Schema(schema, SchemaType.Avro);
            var isCompatible = await _client.IsCompatibleAsync(subject, schemaObj);

            _logger.LogDebug(
                "Schema compatibility checked. Subject: {Subject}, Compatible: {IsCompatible}",
                subject,
                isCompatible);

            return isCompatible;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check schema compatibility for subject {Subject}", subject);
            throw new SchemaRegistryException(
                $"Failed to check schema compatibility for subject '{subject}'",
                ex);
        }
    }

    public async Task SetCompatibilityAsync(
        string subject,
        string compatibility,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: Confluent.SchemaRegistry client doesn't directly support setting compatibility
            // This would require HTTP API calls to the schema registry
            _logger.LogWarning(
                "SetCompatibility not directly supported by client library. Subject: {Subject}, Compatibility: {Compatibility}",
                subject,
                compatibility);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set compatibility for subject {Subject}", subject);
            throw new SchemaRegistryException($"Failed to set compatibility for subject '{subject}'", ex);
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _logger.LogInformation("Schema registry client disposed");
    }
}

/// <summary>
/// Exception thrown when schema registry operations fail.
/// </summary>
public class SchemaRegistryException : Exception
{
    public SchemaRegistryException(string message) : base(message)
    {
    }

    public SchemaRegistryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
