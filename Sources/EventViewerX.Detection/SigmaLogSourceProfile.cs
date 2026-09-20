namespace EventViewerX.Sigma;

/// <summary>
/// Describes a versioned, opt-in mapping from source-neutral Sigma categories to concrete Windows telemetry.
/// </summary>
public sealed class SigmaLogSourceProfile {
    private const string SysmonChannel = "Microsoft-Windows-Sysmon/Operational";
    private const string SysmonProvider = "Microsoft-Windows-Sysmon";
    private const string PowerShellChannel = "Microsoft-Windows-PowerShell/Operational";
    private const string PowerShellProvider = "Microsoft-Windows-PowerShell";
    private readonly IReadOnlyDictionary<string, SigmaLogSourceMapping> mappings;

    /// <summary>Creates a validated, immutable telemetry profile.</summary>
    public SigmaLogSourceProfile(
        string profileId,
        string version,
        IEnumerable<SigmaLogSourceMapping> mappings) {

        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Sigma profile ID cannot be empty.", nameof(profileId));
        }
        if (string.IsNullOrWhiteSpace(version) || !System.Version.TryParse(version, out _)) {
            throw new ArgumentException("Sigma profile version must be a numeric version.", nameof(version));
        }
        ProfileId = profileId.Trim();
        Version = version.Trim();
        SigmaLogSourceMapping[] materialized = (mappings ?? throw new ArgumentNullException(nameof(mappings)))
            .ToArray();
        if (materialized.Length == 0) {
            throw new ArgumentException("A Sigma telemetry profile requires at least one mapping.", nameof(mappings));
        }
        if (materialized.GroupBy(static mapping => mapping.Category, StringComparer.OrdinalIgnoreCase)
            .Any(static group => group.Count() != 1)) {
            throw new ArgumentException("Sigma telemetry profile categories must be unique.", nameof(mappings));
        }
        this.mappings = materialized.ToDictionary(
            static mapping => mapping.Category,
            StringComparer.OrdinalIgnoreCase);
        Mappings = Array.AsReadOnly(materialized
            .OrderBy(static mapping => mapping.Category, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    /// <summary>Gets the stable profile identifier.</summary>
    public string ProfileId { get; }

    /// <summary>Gets the mapping contract version.</summary>
    public string Version { get; }

    /// <summary>Gets the immutable category mappings.</summary>
    public IReadOnlyList<SigmaLogSourceMapping> Mappings { get; }

    /// <summary>
    /// Gets the built-in profile that binds supported endpoint categories to Sysmon and PowerShell telemetry.
    /// Selecting this profile asserts that the corresponding telemetry is enabled and collected.
    /// </summary>
    public static SigmaLogSourceProfile WindowsSysmonAndPowerShell { get; } = CreateWindowsProfile();

    internal bool TryGetMapping(string category, out SigmaLogSourceMapping mapping) =>
        mappings.TryGetValue(category, out mapping!);

    private static SigmaLogSourceProfile CreateWindowsProfile() {
        var mappings = new[] {
            Sysmon("process_creation", 1),
            Sysmon("network_connection", 3),
            Sysmon("sysmon_status", 4, 16),
            Sysmon("driver_load", 6),
            Sysmon("image_load", 7),
            Sysmon("create_remote_thread", 8),
            Sysmon("raw_access_thread", 9),
            Sysmon("process_access", 10),
            Sysmon("file_event", 11),
            Sysmon("registry_add", 12),
            Sysmon("registry_delete", 12),
            Sysmon("registry_event", 12, 13, 14),
            Sysmon("registry_set", 13),
            Sysmon("create_stream_hash", 15),
            Sysmon("pipe_created", 17),
            Sysmon("wmi_event", 19, 20, 21),
            Sysmon("dns_query", 22),
            Sysmon("file_delete", 23, 26),
            Sysmon("process_tampering", 25),
            Sysmon("file_executable_detected", 29),
            Sysmon("sysmon_error", 255),
            PowerShell("ps_module", 4103),
            PowerShell("ps_script", 4104)
        };
        return new SigmaLogSourceProfile(
            "windows-sysmon-powershell",
            "1.0.0",
            mappings);
    }

    private static SigmaLogSourceMapping Sysmon(string category, params int[] eventIds) =>
        new(category, new[] { SysmonChannel }, new[] { SysmonProvider }, eventIds);

    private static SigmaLogSourceMapping PowerShell(string category, params int[] eventIds) =>
        new(category, new[] { PowerShellChannel }, new[] { PowerShellProvider }, eventIds);
}
