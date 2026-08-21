namespace EventViewerX;

/// <summary>
/// Immutable value captured from the provider payload of a Windows event.
/// </summary>
/// <remarks>
/// This owned value type replaces the framework-specific <c>EventProperty</c> wrapper while preserving
/// its public <see cref="Value"/> shape for PowerShell and .NET callers.
/// </remarks>
public sealed class EventPropertyValue {
    /// <summary>
    /// Creates a captured event property value.
    /// </summary>
    /// <param name="value">Typed provider value, or <see langword="null"/> when the payload value is null.</param>
    public EventPropertyValue(object? value) {
        Value = value;
    }

    /// <summary>Typed provider value.</summary>
    public object? Value { get; }
}
