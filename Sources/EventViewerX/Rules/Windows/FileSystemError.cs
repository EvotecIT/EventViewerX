namespace EventViewerX.Rules.Windows;

/// <summary>NTFS reported file-system corruption (55) or a volume requiring action (98).</summary>
public sealed class FileSystemError : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 55, 98 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Ntfs", "Microsoft-Windows-Ntfs" };
    /// <inheritdoc />
    public override EventType Type => EventType.FileSystemError;

    /// <summary>Drive name or volume supplied by NTFS.</summary>
    public string VolumeName;
    /// <summary>NTFS device path.</summary>
    public string DeviceName;
    /// <summary>NTFS action state. Zero denotes a healthy volume and is excluded from this rule.</summary>
    public long? CorruptionActionState;
    /// <summary>Corruption description, when supplied.</summary>
    public string Details;
    /// <summary>Result of the corruption handling, when supplied.</summary>
    public string Outcome;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public FileSystemError(EventObject source) : base(source, nameof(FileSystemError)) {
        VolumeName = Value("DriveName", "NoNameA1");
        DeviceName = Value("DeviceName", "NoNameA0");
        CorruptionActionState = Number("CorruptionActionState");
        Details = Value("Description");
        Outcome = Value("Outcome");
    }

    /// <summary>Excludes routine healthy-volume event 98 records without parsing localized messages.</summary>
    public override bool CanHandle(EventObject source) {
        if (!base.CanHandle(source)) {
            return false;
        }
        return source.Id == 55 || (long.TryParse(source.GetDataValueOrEmpty("CorruptionActionState"),
            System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
            out long state) && state > 0);
    }
}
