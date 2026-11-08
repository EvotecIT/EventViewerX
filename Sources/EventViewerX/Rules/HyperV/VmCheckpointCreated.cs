namespace EventViewerX.Rules.HyperV;

/// <summary>
/// Hyper-V checkpoint created
/// The historical event source has no verified checkpoint-creation contract.
/// </summary>
public class VmCheckpointCreated : EventRuleBase {
    /// <summary>Historical identifiers retained for compatibility; this built-in source is unavailable.</summary>
    public override List<int> EventIds => new() { 4096 };
    /// <summary>Historical channel retained for compatibility; see the catalog definition's UnavailableReason.</summary>
    public override string LogName => "Microsoft-Windows-Hyper-V-VMMS/Admin";
    /// <inheritdoc />
    public override EventType Type => EventType.HyperVCheckpointCreated;

    /// <summary>Rejects the historical source because it does not establish the advertised event.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return false;
    }

    /// <summary>Hyper-V host where the checkpoint was created.</summary>
    public string Computer;
    /// <summary>Name of the virtual machine.</summary>
    public string VmName;
    /// <summary>Name of the created checkpoint.</summary>
    public string CheckpointName;
    /// <summary>User who created the checkpoint.</summary>
    public string User;
    /// <summary>Timestamp of checkpoint creation.</summary>
    public DateTime When;

    /// <summary>Initialises a Hyper-V checkpoint wrapper from an event record.</summary>
    public VmCheckpointCreated(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "VmCheckpointCreated";
        Computer = SourceEvent.ComputerName;
        VmName = SourceEvent.GetValueFromDataDictionary("VmName");
        if (string.IsNullOrEmpty(VmName)) {
            VmName = SourceEvent.GetValueFromDataDictionary("Name");
        }
        if (string.IsNullOrEmpty(VmName)) {
            VmName = SourceEvent.GetValueFromDataDictionary("VMName");
        }

        CheckpointName = SourceEvent.GetValueFromDataDictionary("CheckpointName");
        if (string.IsNullOrEmpty(CheckpointName)) {
            CheckpointName = SourceEvent.GetValueFromDataDictionary("SnapshotName");
        }
        if (string.IsNullOrEmpty(CheckpointName)) {
            CheckpointName = SourceEvent.GetValueFromDataDictionary("Name", "FriendlyName");
        }

        User = SourceEvent.GetValueFromDataDictionary("User");
        if (string.IsNullOrEmpty(User)) {
            User = SourceEvent.GetValueFromDataDictionary("UserName");
        }
        if (string.IsNullOrEmpty(User)) {
            User = SourceEvent.GetValueFromDataDictionary("AccountName");
        }
        if (string.IsNullOrEmpty(User)) {
            User = SourceEvent.GetSubjectAccountOrEmpty();
        }

        When = SourceEvent.TimeCreated;
    }
}


