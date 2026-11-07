namespace EventViewerX.Rules.Windows;

/// <summary>The NVMe storage driver issued a controller/device reset (129).</summary>
public sealed class StorageReset : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 129 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "stornvme" };
    /// <inheritdoc />
    public override EventType Type => EventType.StorageReset;

    /// <summary>Storage port or device path. A RAID-port path does not identify a specific disk or volume.</summary>
    public string DeviceName;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public StorageReset(EventObject source) : base(source, nameof(StorageReset)) {
        DeviceName = Value("DeviceName", "NoNameA0");
    }
}
