namespace EventViewerX;

/// <summary>Compiled construction and matching behavior for one typed event projection.</summary>
internal sealed class EventProjectorDefinition {
    internal EventProjectorDefinition(
        EventType type,
        string name,
        Func<EventObject, EventTypeRecord> factory,
        Func<EventObject, bool>? precondition) {

        Type = type;
        Name = name;
        Factory = factory;
        Precondition = precondition;
    }

    internal EventType Type { get; }
    internal string Name { get; }
    internal Func<EventObject, EventTypeRecord> Factory { get; }
    internal Func<EventObject, bool>? Precondition { get; }
}
