namespace EventViewerX.Rules.Windows;

/// <summary>
/// BitLocker protection key changed or backed up
/// 4673: A privileged service was called
/// 4692: Backup of data protection master key was attempted
/// </summary>
public class BitLockerKeyChange : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4673, 4692 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.BitLockerKeyChange;

    /// <summary>Accepts BitLocker key change/backup events in the Security log.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }
    /// <summary>Computer where the key operation happened.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Volume type.</summary>
    public BitLockerVolumeType? Volume;
    /// <summary>Protector type used.</summary>
    public BitLockerProtectorType? ProtectorType;
    /// <summary>Master key identifier.</summary>
    public string MasterKeyId;
    /// <summary>Recovery key identifier.</summary>
    public string RecoveryKeyId;
    /// <summary>Server where recovery key was stored.</summary>
    public string RecoveryServer;
    /// <summary>User responsible for the change.</summary>
    public string Who;
    /// <summary>Time of the event.</summary>
    public DateTime When;

    /// <summary>Initialises a BitLocker key change wrapper from an event record.</summary>
    public BitLockerKeyChange(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "BitLockerKeyChange";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        Volume = EventsHelper.GetBitLockerVolumeType(
            SourceEvent.GetValueFromDataDictionary("VolumeName", "Volume"));
        ProtectorType = EventsHelper.GetBitLockerProtectorType(
            SourceEvent.GetValueFromDataDictionary("ProtectorType", "KeyProtection"));
        MasterKeyId = SourceEvent.GetValueFromDataDictionary("MasterKeyId");
        RecoveryKeyId = SourceEvent.GetValueFromDataDictionary("RecoveryKeyId");
        RecoveryServer = SourceEvent.GetValueFromDataDictionary("RecoveryServer");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



