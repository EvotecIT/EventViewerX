namespace EventViewerX.Rules.HyperV;

/// <summary>
/// Hyper-V VM shutdown event
/// 18502: The virtual machine was turned off
/// </summary>
public class VmShutdown : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 18502 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-Hyper-V-Worker-Admin";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-Hyper-V-Worker" };
    /// <inheritdoc />
    public override EventType Type => EventType.HyperVVirtualMachineShutdown;

    /// <summary>Accepts Hyper-V VM shutdown events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Hyper-V-Worker");
    }

    /// <summary>Hyper-V host where the VM was shut down.</summary>
    public string Computer;
    /// <summary>Name of the virtual machine.</summary>
    public string VmName;
    /// <summary>Unique identifier of the virtual machine.</summary>
    public string VirtualMachineId;
    /// <summary>User or account that initiated the shutdown.</summary>
    public string User;
    /// <summary>Timestamp of the shutdown event.</summary>
    public DateTime When;

    /// <summary>Initialises a Hyper-V VM shutdown wrapper from an event record.</summary>
    public VmShutdown(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "HyperVVirtualMachineShutdown";
        Computer = SourceEvent.ComputerName;
        VirtualMachineId = Value("VmId", "VMId", "VirtualMachineId");
        VmName = SourceEvent.GetValueFromDataDictionary("VmName");
        if (string.IsNullOrEmpty(VmName)) {
            VmName = SourceEvent.GetValueFromDataDictionary("Name");
        }
        if (string.IsNullOrEmpty(VmName)) {
            VmName = SourceEvent.GetValueFromDataDictionary("VMName");
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


