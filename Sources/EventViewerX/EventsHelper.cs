using System.Globalization;

namespace EventViewerX;

/// <summary>
/// Event helper methods.
/// </summary>

internal static class EventsHelper {
    /// <summary>
    /// Translates a string value to an ImpersonationLevel enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated ImpersonationLevel enum.</returns>
    public static ImpersonationLevel? GetImpersonationLevel(string value) {
        if (Enum.TryParse(value.TrimStart('%', '%'), out ImpersonationLevel impersonationLevel)) {
            return impersonationLevel;
        }
        return null;
    }

    /// <summary>
    /// Translates a string value to a VirtualAccount enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated VirtualAccount enum.</returns>
    public static VirtualAccount? GetVirtualAccount(string value) {
        if (Enum.TryParse(value.TrimStart('%', '%'), out VirtualAccount virtualAccount)) {
            return virtualAccount;
        }
        return null;
    }

    /// <summary>
    /// Translates a string value to an ElevatedToken enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated ElevatedToken enum.</returns>
    public static ElevatedToken? GetElevatedToken(string value) {
        if (Enum.TryParse(value.TrimStart('%', '%'), out ElevatedToken elevatedToken)) {
            return elevatedToken;
        }
        return null;
    }

    /// <summary>
    /// Translates a string value to a LogonType enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated LogonType enum.</returns>
    public static LogonType? GetLogonType(string value) {
        if (Enum.TryParse(value, out LogonType logonType)) {
            return logonType;
        }
        return null;
    }


    /// <summary>
    /// Translates a string value to a TicketOptions enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated TicketOptions enum.</returns>
    public static TicketOptions? GetTicketOptions(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            trimmed = trimmed.Substring(2);
        }

        if (uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var numeric)) {
            return (TicketOptions)numeric;
        }

        if (Enum.TryParse(trimmed, true, out TicketOptions ticketOptions)) {
            return ticketOptions;
        }

        return null;
    }

    /// <summary>
    /// Translates a status string (often hex like 0x0) to a StatusCode enum.
    /// </summary>
    public static StatusCode? GetStatusCode(string value) {
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Normalize hex like "0xC000006D" or "c000006d"
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            trimmed = trimmed.Substring(2);
        }

        if (uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) {
            return Enum.IsDefined(typeof(StatusCode), code) ? (StatusCode)code : null;
        }

        // Fall back to name-based parsing (rare)
        if (Enum.TryParse(trimmed, ignoreCase: true, out StatusCode parsed)) {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// Translates a string value to a TicketEncryptionType enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated TicketEncryptionType enum.</returns>
    public static TicketEncryptionType? GetTicketEncryptionType(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            trimmed = trimmed.Substring(2);
        }

        if (uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var numeric)) {
            return (TicketEncryptionType)numeric;
        }

        if (Enum.TryParse(trimmed, true, out TicketEncryptionType ticketEncryptionType)) {
            return ticketEncryptionType;
        }

        return null;
    }

    /// <summary>
    /// Parses an <c>msDS-SupportedEncryptionTypes</c> value from a Kerberos
    /// event while preserving unknown flag bits.
    /// </summary>
    /// <param name="value">
    /// A hexadecimal or decimal value, optionally followed by a provider
    /// description such as <c>0x1C (RC4, AES128-SHA96, AES256-SHA96)</c>.
    /// </param>
    /// <returns>
    /// Parsed flags, including <see cref="KerberosSupportedEncryptionTypes.NotDefined"/>
    /// for an explicit zero; <c>null</c> when the field is missing or cannot be parsed.
    /// </returns>
    public static KerberosSupportedEncryptionTypes? GetKerberosSupportedEncryptionTypes(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Equals("-", StringComparison.Ordinal) ||
            trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        int separator = trimmed.IndexOfAny(new[] { ' ', '\t', '(' });
        string token = separator >= 0 ? trimmed.Substring(0, separator) : trimmed;
        NumberStyles style = NumberStyles.Integer;
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            token = token.Substring(2);
            style = NumberStyles.HexNumber;
        }

        if (uint.TryParse(token, style, CultureInfo.InvariantCulture, out uint numeric)) {
            return (KerberosSupportedEncryptionTypes)numeric;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out KerberosSupportedEncryptionTypes parsed)
            ? parsed
            : null;
    }

    /// <summary>Returns whether a parsed supported-encryption mask includes AES.</summary>
    /// <param name="value">Parsed supported-encryption flags, or <c>null</c> when the event did not provide them.</param>
    /// <returns><c>null</c> for missing evidence; otherwise whether any AES flag is present.</returns>
    public static bool? SupportsKerberosAes(KerberosSupportedEncryptionTypes? value) {
        if (!value.HasValue) {
            return null;
        }

        const KerberosSupportedEncryptionTypes aes =
            KerberosSupportedEncryptionTypes.Aes128CtsHmacSha1 |
            KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1 |
            KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1SessionKey |
            KerberosSupportedEncryptionTypes.Aes128CtsHmacSha256 |
            KerberosSupportedEncryptionTypes.Aes256CtsHmacSha384;
        return (value.Value & aes) != 0;
    }

    /// <summary>Returns whether a parsed supported-encryption mask includes RC4.</summary>
    /// <param name="value">Parsed supported-encryption flags, or <c>null</c> when the event did not provide them.</param>
    /// <returns><c>null</c> for missing evidence; otherwise whether RC4 is present.</returns>
    public static bool? SupportsKerberosRc4(KerberosSupportedEncryptionTypes? value) {
        return value.HasValue
            ? (value.Value & KerberosSupportedEncryptionTypes.Rc4Hmac) != 0
            : null;
    }

    /// <summary>Returns whether a ticket or session-key encryption type is DES or RC4.</summary>
    public static bool IsWeakKerberosEncryption(TicketEncryptionType? value) {
        return value is TicketEncryptionType.DES_CBC_CRC
            or TicketEncryptionType.DES_CBC_MD5
            or TicketEncryptionType.RC4_HMAC
            or TicketEncryptionType.RC4_HMAC_EXP;
    }

    /// <summary>
    /// Translates a string value to a PreAuthType enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated PreAuthType enum.</returns>
    public static PreAuthType? GetPreAuthType(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            trimmed = trimmed.Substring(2);
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
            && Enum.IsDefined(typeof(PreAuthType), numeric)) {
            return (PreAuthType)numeric;
        }

        if (Enum.TryParse(trimmed, true, out PreAuthType preAuthType)) {
            return preAuthType;
        }

        return null;
    }

    public static string DescribeTicketOptions(TicketOptions? options, string rawValue)
    {
        if (options.HasValue) {
            var names = Rules.RuleHelpers.DescribeFlags(options);
            var hex = $"0x{((uint)options.Value):X8}";
            return string.IsNullOrWhiteSpace(names)
                ? hex
                : $"{names} ({hex})";
        }
        return rawValue ?? string.Empty;
    }

    public static string DescribeStatus(StatusCode? status, string rawValue)
    {
        if (status.HasValue) {
            var hex = $"0x{((uint)status.Value):X8}";
            return $"{status} ({hex})";
        }
        return rawValue ?? string.Empty;
    }

    public static string DescribePreAuthType(PreAuthType? preAuth, string rawValue)
    {
        if (preAuth.HasValue) {
            var description = preAuth switch
            {
                PreAuthType.None => "None",
                PreAuthType.EncTimestamp => "Password / ENC-TIMESTAMP",
                PreAuthType.PublicKeyInitial => "Smartcard / PKINIT",
                PreAuthType.PublicKeyResponse => "PKINIT Response",
                PreAuthType.Fast => "FAST / Armoring",
                PreAuthType.EncryptedChallenge => "Encrypted Challenge",
                _ => preAuth.Value.ToString()
            };
            return $"{description} ({(int)preAuth.Value})";
        }
        return rawValue ?? string.Empty;
    }

    public static string DescribeEncryption(TicketEncryptionType? etype, string rawValue)
    {
        if (etype.HasValue) {
            var hex = $"0x{((uint)etype.Value):X}";
            return $"{etype} ({hex})";
        }
        return rawValue ?? string.Empty;
    }

    /// <summary>
    /// Translates a string value to a BitLockerProtectorType enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated BitLockerProtectorType enum.</returns>
    public static BitLockerProtectorType? GetBitLockerProtectorType(string value) {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }

        if (value.StartsWith("%%", StringComparison.Ordinal)) {
            value = value.Trim('%');
        }

        if (int.TryParse(value, out var number)
            && Enum.IsDefined(typeof(BitLockerProtectorType), number)) {
            return (BitLockerProtectorType)number;
        }

        return null;
    }

    /// <summary>
    /// Translates a string value to a BitLockerVolumeType enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated BitLockerVolumeType enum.</returns>
    public static BitLockerVolumeType? GetBitLockerVolumeType(string value) {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }

        if (value.StartsWith("%%", StringComparison.Ordinal)) {
            value = value.Trim('%');
        }

        if (int.TryParse(value, out var number)
            && Enum.IsDefined(typeof(BitLockerVolumeType), number)) {
            return (BitLockerVolumeType)number;
        }

        return null;
    }

    /// <summary>
    /// Translates a string value to a NasPortType enum.
    /// </summary>
    /// <param name="value">The value to translate.</param>
    /// <returns>The translated NasPortType enum.</returns>
    public static NasPortType? GetNasPortType(string value) {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }

        if (value.StartsWith("%%", StringComparison.Ordinal)) {
            value = value.Trim('%');
        }

        if (int.TryParse(value, out var number)
            && Enum.IsDefined(typeof(NasPortType), number)) {
            return (NasPortType)number;
        }

        var normalized = value.Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty);

        if (Enum.TryParse(normalized, true, out NasPortType result)) {
            return result;
        }

        return null;
    }

    /// <summary>
    /// Translates device class name into a simplified device type.
    /// </summary>
    /// <param name="className">Device class name.</param>
    /// <returns>Device type string.</returns>
    public static string TranslateDeviceType(string className) {
        if (string.IsNullOrEmpty(className)) {
            return string.Empty;
        }

        if (className.Equals("DiskDrive", StringComparison.OrdinalIgnoreCase)) {
            return "Disk Drive";
        }

        if (className.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "USB Device";
        }

        if (className.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "Network Adapter";
        }

        if (className.IndexOf("Image", StringComparison.OrdinalIgnoreCase) >= 0
            || className.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "Imaging Device";
        }

        return className;
    }

    private static readonly Dictionary<string, string> VendorLookup = new() {
        { "045E", "Microsoft" },
        { "046D", "Logitech" },
        { "0781", "SanDisk" },
        { "05AC", "Apple" },
        { "0BDA", "Realtek" }
    };

    private static readonly Dictionary<string, string> PrivilegeLookup = new() {
        { "SeTrustedCredManAccessPrivilege", "Access Credential Manager as a trusted caller" },
        { "SeNetworkLogonRight", "Access this computer from the network" },
        { "SeTcbPrivilege", "Act as part of the operating system" },
        { "SeMachineAccountPrivilege", "Add workstations to domain" },
        { "SeIncreaseQuotaPrivilege", "Adjust memory quotas for a process" },
        { "SeInteractiveLogonRight", "Allow log on locally" },
        { "SeRemoteInteractiveLogonRight", "Allow log on through Remote Desktop Services" },
        { "SeBackupPrivilege", "Back up files and directories" },
        { "SeChangeNotifyPrivilege", "Bypass traverse checking" },
        { "SeSystemtimePrivilege", "Change the system time" },
        { "SeTimeZonePrivilege", "Change the time zone" },
        { "SeCreatePagefilePrivilege", "Create a pagefile" },
        { "SeCreateTokenPrivilege", "Create a token object" },
        { "SeCreateGlobalPrivilege", "Create global objects" },
        { "SeCreatePermanentPrivilege", "Create permanent shared objects" },
        { "SeCreateSymbolicLinkPrivilege", "Create symbolic links" },
        { "SeDebugPrivilege", "Debug programs" },
        { "SeDenyNetworkLogonRight", "Deny access to this computer from the network" },
        { "SeDenyBatchLogonRight", "Deny log on as a batch job" },
        { "SeDenyServiceLogonRight", "Deny log on as a service" },
        { "SeDenyInteractiveLogonRight", "Deny log on locally" },
        { "SeDenyRemoteInteractiveLogonRight", "Deny log on through Remote Desktop Services" },
        { "SeEnableDelegationPrivilege", "Enable computer and user accounts to be trusted for delegation" },
        { "SeRemoteShutdownPrivilege", "Force shutdown from a remote system" },
        { "SeAuditPrivilege", "Generate security audits" },
        { "SeImpersonatePrivilege", "Impersonate a client after authentication" },
        { "SeIncreaseWorkingSetPrivilege", "Increase a process working set" },
        { "SeIncreaseBasePriorityPrivilege", "Increase scheduling priority" },
        { "SeLoadDriverPrivilege", "Load and unload device drivers" },
        { "SeLockMemoryPrivilege", "Lock pages in memory" },
        { "SeBatchLogonRight", "Log on as a batch job" },
        { "SeServiceLogonRight", "Log on as a service" },
        { "SeSecurityPrivilege", "Manage auditing and security log" },
        { "SeRelabelPrivilege", "Modify an object label" },
        { "SeSystemEnvironmentPrivilege", "Modify firmware environment values" },
        { "SeManageVolumePrivilege", "Perform volume maintenance tasks" },
        { "SeProfileSingleProcessPrivilege", "Profile single process" },
        { "SeSystemProfilePrivilege", "Profile system performance" },
        { "SeUndockPrivilege", "Remove computer from docking station" },
        { "SeAssignPrimaryTokenPrivilege", "Replace a process level token" },
        { "SeRestorePrivilege", "Restore files and directories" },
        { "SeShutdownPrivilege", "Shut down the system" },
        { "SeSyncAgentPrivilege", "Synchronize directory service data" },
        { "SeTakeOwnershipPrivilege", "Take ownership of files or other objects" }
    };

    public static string TranslatePrivilege(string privilege) {
        if (string.IsNullOrEmpty(privilege)) {
            return string.Empty;
        }

        return PrivilegeLookup.TryGetValue(privilege, out var friendly)
            ? friendly
            : privilege;
    }

    /// <summary>
    /// Attempts to translate hardware vendor identifiers to a friendly name.
    /// </summary>
    /// <param name="vendorIds">Vendor identifiers string.</param>
    /// <returns>Vendor name if recognized; otherwise original value.</returns>
    public static string TranslateVendor(string vendorIds) {
        if (string.IsNullOrEmpty(vendorIds)) {
            return string.Empty;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            vendorIds,
            @"VID_([0-9A-Fa-f]{4})|VEN_([0-9A-Fa-f]{4})");
        if (match.Success) {
            var vid = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (VendorLookup.TryGetValue(vid.ToUpperInvariant(), out var name)) {
                return name;
            }
        }

        var scsiMatch = System.Text.RegularExpressions.Regex.Match(
            vendorIds,
            @"SCSI\\\\Disk(?<vendor>[^\\_]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (scsiMatch.Success) {
            return scsiMatch.Groups["vendor"].Value;
        }

        return vendorIds.Replace('_', ' ');
    }
}
