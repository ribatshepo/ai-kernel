namespace AIKernel.Core.EventBus.Consumers;

/// <summary>
/// Registry for mapping event types to their handlers.
/// </summary>
public interface IEventHandlerRegistry
{
    /// <summary>
    /// Registers a handler type for a specific event type.
    /// </summary>
    /// <typeparam name="TData">The event data type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    void Register<TData, THandler>()
        where TData : class
        where THandler : IEventHandler<TData>;

    /// <summary>
    /// Gets the handler type for a specific event type name.
    /// </summary>
    /// <param name="eventTypeName">The event type name.</param>
    /// <returns>The handler type if registered, null otherwise.</returns>
    Type? GetHandlerType(string eventTypeName);

    /// <summary>
    /// Gets the event data type for a specific event type name.
    /// </summary>
    /// <param name="eventTypeName">The event type name.</param>
    /// <returns>The event data type if registered, null otherwise.</returns>
    Type? GetEventDataType(string eventTypeName);

    /// <summary>
    /// Checks if a handler is registered for the specified event type.
    /// </summary>
    /// <param name="eventTypeName">The event type name.</param>
    /// <returns>True if a handler is registered, false otherwise.</returns>
    bool IsRegistered(string eventTypeName);
}
