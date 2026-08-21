using System.Net.NetworkInformation;

namespace EventViewerX;

/// <summary>Normalizes and classifies local and remote Windows Event Log targets.</summary>
public static class EventLogTarget {
    private static readonly Lazy<string> LocalMachineNameValue =
        new(ResolveLocalMachineName);

    /// <summary>Fully qualified current host name, or the machine name when DNS resolution is unavailable.</summary>
    public static string LocalMachineName =>
        LocalMachineNameValue.Value;

    /// <summary>Returns true when a computer name identifies the current Windows host.</summary>
    public static bool IsLocalMachine(string? machineName) {
        if (string.IsNullOrWhiteSpace(machineName)) {
            return true;
        }
        string name =
            machineName!.Trim();
        if (name == ".") {
            return true;
        }
        name = name.TrimEnd('.');
        return name.Equals(
                   "localhost",
                   StringComparison.OrdinalIgnoreCase) ||
               name.Equals(
                   Environment.MachineName,
                   StringComparison.OrdinalIgnoreCase) ||
               name.Equals(
                   LocalMachineName,
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trims and case-insensitively deduplicates targets while representing every
    /// spelling of the local computer as one null target.
    /// </summary>
    public static IReadOnlyList<string?> NormalizeMachineNames(
        IEnumerable<string?>? machineNames) {

        string?[] supplied =
            machineNames?.ToArray() ??
            Array.Empty<string?>();
        if (supplied.Length == 0) {
            return new string?[] { null };
        }
        var normalized =
            new List<string?>();
        var seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
        foreach (string? machineName in
                 supplied) {
            string? target =
                IsLocalMachine(machineName)
                    ? null
                    : machineName!.Trim();
            string key =
                target ?? "<LOCAL>";
            if (seen.Add(key)) {
                normalized.Add(target);
            }
        }
        return normalized;
    }

    private static string ResolveLocalMachineName() {
        try {
            return BuildLocalMachineName(
                Environment.MachineName,
                IPGlobalProperties
                    .GetIPGlobalProperties()
                    .DomainName);
        } catch {
            return Environment.MachineName;
        }
    }

    internal static string BuildLocalMachineName(
        string machineName,
        string? domainName) {

        string normalizedMachine =
            machineName.Trim().TrimEnd('.');
        string normalizedDomain =
            domainName?.Trim().Trim('.') ??
            string.Empty;
        if (normalizedDomain.Length == 0 ||
            normalizedMachine.EndsWith(
                "." + normalizedDomain,
                StringComparison.OrdinalIgnoreCase)) {
            return normalizedMachine;
        }
        return normalizedMachine +
               "." +
               normalizedDomain;
    }
}
