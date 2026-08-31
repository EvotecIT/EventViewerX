namespace EventViewerX;

/// <summary>Provides one native detection definition to the compiled detection engine.</summary>
public interface IEventDetectionRule {
    /// <summary>Detached rule definition.</summary>
    EventDetectionRuleDefinition Definition { get; }
}

/// <summary>Immutable native implementation of <see cref="IEventDetectionRule"/>.</summary>
public sealed class EventDetectionRule : IEventDetectionRule {
    private readonly EventDetectionRuleDefinition _definition;

    /// <summary>Creates a rule from a validated detached definition.</summary>
    public EventDetectionRule(EventDetectionRuleDefinition definition) {
        _definition = definition?.Snapshot() ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <inheritdoc />
    public EventDetectionRuleDefinition Definition => _definition.Snapshot();
}
