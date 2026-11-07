namespace EventViewerX.Rules.Windows;

/// <summary>WHEA reported corrected PCI Express, processor or memory errors, or a fatal processor error.</summary>
public sealed class HardwareError : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 17, 18, 19, 47 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-WHEA-Logger" };
    /// <inheritdoc />
    public override EventType Type => EventType.HardwareError;

    /// <summary>PCI Express, Processor Core or Memory, as identified by the event schema.</summary>
    public string Component;
    /// <summary>True for corrected events 17, 19 and 47.</summary>
    public bool IsCorrected;
    /// <summary>True for fatal processor event 18.</summary>
    public bool IsFatal;
    /// <summary>Raw WHEA error-source value.</summary>
    public string ErrorSource;
    /// <summary>Raw processor error-type value, when present.</summary>
    public string ErrorType;
    /// <summary>Processor APIC identifier, when present.</summary>
    public string ProcessorApicId;
    /// <summary>Primary PCI device name, when present.</summary>
    public string DeviceName;
    /// <summary>Reported memory address, when present.</summary>
    public string PhysicalAddress;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public HardwareError(EventObject source) : base(source, nameof(HardwareError)) {
        Component = source.Id switch { 17 => "PCI Express", 47 => "Memory", _ => "Processor Core" };
        IsCorrected = source.Id != 18;
        IsFatal = source.Id == 18;
        ErrorSource = Value("ErrorSource");
        ErrorType = Value("ErrorType");
        ProcessorApicId = Value("ApicId");
        DeviceName = Value("PrimaryDeviceName");
        PhysicalAddress = Value("PhysicalAddress");
    }
}
