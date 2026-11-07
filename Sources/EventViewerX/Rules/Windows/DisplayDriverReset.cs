namespace EventViewerX.Rules.Windows;

/// <summary>Windows recovered a display driver that stopped responding (4101).</summary>
public sealed class DisplayDriverReset : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4101 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Display" };
    /// <inheritdoc />
    public override EventType Type => EventType.DisplayDriverReset;

    /// <summary>Display driver that Windows reported as recovered.</summary>
    public string DriverName;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public DisplayDriverReset(EventObject source) : base(source, nameof(DisplayDriverReset)) {
        DriverName = Value("DriverName", "NoNameA0");
    }
}
