namespace EventViewerX.Rules.Windows;

/// <summary>
/// BitLocker protection suspended
/// 773: BitLocker was suspended for a volume
/// </summary>
public class BitLockerSuspended : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 773 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-BitLocker/BitLocker Management";
    /// <inheritdoc />
    public override EventType Type => EventType.BitLockerSuspended;

    /// <summary>Accepts suspension events from the BitLocker API provider.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-BitLocker-API");
    }
    /// <summary>Machine where protection was suspended.</summary>
    public string Computer;
    /// <summary>Volume affected.</summary>
    public BitLockerVolumeType? Volume;
    /// <summary>Account that suspended protection.</summary>
    public string Who;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises a BitLocker suspended wrapper from an event record.</summary>
    public BitLockerSuspended(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "BitLockerSuspended";
        Computer = SourceEvent.ComputerName;
        Volume = EventsHelper.GetBitLockerVolumeType(
            SourceEvent.GetValueFromDataDictionary("VolumeName", "Volume"));
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}


