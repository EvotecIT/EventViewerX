namespace EventViewerX.Rules.Windows;

/// <summary>Disk reported a bad block (7), paging I/O error (51) or retried I/O (153).</summary>
public sealed class DiskError : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 7, 51, 153 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "disk" };
    /// <inheritdoc />
    public override EventType Type => EventType.DiskError;

    /// <summary>Device path supplied by the disk driver.</summary>
    public string DeviceName;
    /// <summary>Logical block address for retried I/O, preserved without guessing its numeric base.</summary>
    public string LogicalBlockAddress;
    /// <summary>Disk number for retried I/O, when present.</summary>
    public long? DiskNumber;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public DiskError(EventObject source) : base(source, nameof(DiskError)) {
        DeviceName = Value("DeviceName", "NoNameA0");
        LogicalBlockAddress = source.Id == 153 ? Value("Lba", "NoNameA1") : string.Empty;
        DiskNumber = source.Id == 153 ? Number("DiskNumber", "NoNameA2") : null;
    }
}
