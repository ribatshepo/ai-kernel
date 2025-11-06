using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AIKernel.Core.EventBus.Consumers;

/// <summary>
/// Thread-safe registry for event handlers.
/// </summary>
public class EventHandlerRegistry : IEventHandlerRegistry
{
    private readonly ConcurrentDictionary<string, HandlerRegistration> _registrations = new();
    private readonly ILogger<EventHandlerRegistry> _logger;

    public EventHandlerRegistry(ILogger<EventHandlerRegistry> logger)
    {
        _logger = logger;
    }

    public void Register<TData, THandler>()
        where TData : class
        where THandler : IEventHandler<TData>
    {
        var eventTypeName = typeof(TData).Name;
        var handlerType = typeof(THandler);
        var eventDataType = typeof(TData);

        var registration = new HandlerRegistration
        {
            EventTypeName = eventTypeName,
            EventDataType = eventDataType,
            HandlerType = handlerType
        };

        if (_registrations.TryAdd(eventTypeName, registration))
        {
            _logger.LogInformation(
                "Registered event handler. EventType: {EventType}, Handler: {Handler}",
                eventTypeName,
                handlerType.Name);
        }
        else
        {
            _logger.LogWarning(
                "Event handler already registered for event type {EventType}. Existing handler: {ExistingHandler}, New handler: {NewHandler}",
                eventTypeName,
                _registrations[eventTypeName].HandlerType.Name,
                handlerType.Name);

            throw new InvalidOperationException(
                $"A handler is already registered for event type '{eventTypeName}'. " +
                $"Existing: {_registrations[eventTypeName].HandlerType.Name}, " +
                $"New: {handlerType.Name}");
        }
    }

    public Type? GetHandlerType(string eventTypeName)
    {
        return _registrations.TryGetValue(eventTypeName, out var registration)
            ? registration.HandlerType
            : null;
    }

    public Type? GetEventDataType(string eventTypeName)
    {
        return _registrations.TryGetValue(eventTypeName, out var registration)
            ? registration.EventDataType
            : null;
    }

    public bool IsRegistered(string eventTypeName)
    {
        return _registrations.ContainsKey(eventTypeName);
    }

    private class HandlerRegistration
    {
        public string EventTypeName { get; set; } = string.Empty;
        public Type EventDataType { get; set; } = null!;
        public Type HandlerType { get; set; } = null!;
    }
}
