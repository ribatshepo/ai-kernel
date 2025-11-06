namespace AIKernel.Core.EventBus.SchemaRegistry;

/// <summary>
/// Interface for schema registry operations.
/// </summary>
public interface ISchemaRegistry
{
    /// <summary>
    /// Registers a schema for the specified subject.
    /// </summary>
    /// <param name="subject">The subject name (usually topic-value or topic-key).</param>
    /// <param name="schema">The Avro schema definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema ID.</returns>
    Task<int> RegisterSchemaAsync(
        string subject,
        string schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a schema by ID.
    /// </summary>
    /// <param name="schemaId">The schema ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema definition.</returns>
    Task<string> GetSchemaAsync(
        int schemaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest schema for a subject.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema ID and definition.</returns>
    Task<(int Id, string Schema)> GetLatestSchemaAsync(
        string subject,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all versions of a schema for a subject.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of schema versions.</returns>
    Task<IEnumerable<int>> GetSchemaVersionsAsync(
        string subject,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a schema is compatible with the subject's compatibility setting.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="schema">The schema to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if compatible, false otherwise.</returns>
    Task<bool> CheckCompatibilityAsync(
        string subject,
        string schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the compatibility level for a subject.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="compatibility">The compatibility level (BACKWARD, FORWARD, FULL, NONE).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetCompatibilityAsync(
        string subject,
        string compatibility,
        CancellationToken cancellationToken = default);
}
