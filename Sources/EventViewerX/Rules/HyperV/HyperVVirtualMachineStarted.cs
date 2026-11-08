namespace EventViewerX.Rules.HyperV;

/// <summary>
/// Hyper-V virtual machine started
/// 18500: The virtual machine was started
/// </summary>
public class HyperVVirtualMachineStarted : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 18500 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-Hyper-V-Worker-Admin";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-Hyper-V-Worker" };
    /// <inheritdoc />
    public override EventType Type => EventType.HyperVVirtualMachineStarted;

    /// <summary>Accepts Hyper-V VM start events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Hyper-V-Worker");
    }

    /// <summary>Hyper-V host that started the VM.</summary>
    public string Computer;
    /// <summary>Name of the virtual machine.</summary>
    public string VirtualMachine;
    /// <summary>Unique identifier of the virtual machine.</summary>
    public string VirtualMachineId;
    /// <summary>Timestamp when the VM was started.</summary>
    public DateTime When;

    /// <summary>Initialises a Hyper-V VM start wrapper from an event record.</summary>
    public HyperVVirtualMachineStarted(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "HyperVVirtualMachineStarted";
        Computer = SourceEvent.ComputerName;
        VirtualMachine = Value("VmName", "VMName", "Name");
        VirtualMachineId = Value("VmId", "VMId", "VirtualMachineId");
        When = SourceEvent.TimeCreated;
    }
}
