namespace EventViewerX.Rules.Windows;

/// <summary>
/// BitLocker protection key changed or backed up
/// Key protector creation/removal and recovery information backups from the BitLocker API provider.
/// </summary>
public class BitLockerKeyChange : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 775, 776, 784, 828, 845, 897 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-BitLocker/BitLocker Management";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-BitLocker-API" };
    /// <inheritdoc />
    public override EventType Type => EventType.BitLockerKeyChange;

    /// <summary>Accepts BitLocker key protector changes and successful recovery information backups.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-BitLocker-API");
    }
    /// <summary>Computer where the key operation happened.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Volume type.</summary>
    public BitLockerVolumeType? Volume;
    /// <summary>Volume path supplied by the provider.</summary>
    public string VolumeName;
    /// <summary>Drive letter or mount point supplied by the provider.</summary>
    public string VolumeMountPoint;
    /// <summary>Identifier of the affected key protector.</summary>
    public string ProtectorId;
    /// <summary>Directory or account service that received recovery information, when this is a backup event.</summary>
    public string RecoveryDestination;
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
        Action = SourceEvent.Id switch {
            775 => "Key protector created",
            776 => "Key protector removed",
            _ => "Recovery information backed up"
        };
        VolumeName = Value("VolumeName", "VolumeGUID");
        VolumeMountPoint = Value("VolumeMountPoint");
        ProtectorId = Value("ProtectorGUID");
        RecoveryDestination = SourceEvent.Id switch {
            784 => "Active Directory Domain Services",
            828 or 897 => "Microsoft account",
            845 => "Microsoft Entra ID",
            _ => string.Empty
        };
        Volume = EventsHelper.GetBitLockerVolumeType(
            Value("VolumeType", "Volume"));
        string protectorType = Value("ProtectorType", "KeyProtection");
        if (uint.TryParse(protectorType.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? protectorType.Substring(2) : protectorType,
            protectorType.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out uint nativeType)) {
            ProtectorType = nativeType switch {
                1 => BitLockerProtectorType.Tpm,
                2 => BitLockerProtectorType.ExternalKey,
                3 => BitLockerProtectorType.NumericalPassword,
                4 => BitLockerProtectorType.TpmPin,
                5 => BitLockerProtectorType.TpmStartupKey,
                6 => BitLockerProtectorType.TpmPinAndStartupKey,
                8 => BitLockerProtectorType.Passphrase,
                _ => EventsHelper.GetBitLockerProtectorType(protectorType)
            };
        } else {
            ProtectorType = EventsHelper.GetBitLockerProtectorType(protectorType);
        }
        MasterKeyId = SourceEvent.GetValueFromDataDictionary("MasterKeyId");
        RecoveryKeyId = Value("RecoveryKeyId");
        if (RecoveryKeyId.Length == 0 && (RecoveryDestination.Length > 0 || ProtectorType == BitLockerProtectorType.NumericalPassword)) {
            RecoveryKeyId = ProtectorId;
        }
        RecoveryServer = SourceEvent.GetValueFromDataDictionary("RecoveryServer");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



