using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Summary report for Windows Security logon-related events (typically 4624/4625/4634/4647).
/// </summary>
internal sealed class SecurityUserLogonsReport {
    /// <summary>Number of scanned events passed into the builder.</summary>
    public int Scanned { get; set; }
    /// <summary>True when at least one additional matching event existed beyond the requested cap.</summary>
    public bool Truncated { get; set; }
    /// <summary>Number of matched events (typically equals <see cref="Scanned"/> unless caller filters).</summary>
    public int Matched { get; set; }
    /// <summary>Minimum event time (UTC) among matched events.</summary>
    public DateTime? MinUtc { get; set; }
    /// <summary>Maximum event time (UTC) among matched events.</summary>
    public DateTime? MaxUtc { get; set; }

    /// <summary>Event IDs included by the caller.</summary>
    public IReadOnlyList<int> EventIds { get; set; } = Array.Empty<int>();

    /// <summary>Counts by event ID.</summary>
    public IReadOnlyDictionary<int, long> ByEventId { get; set; } = new Dictionary<int, long>();
    /// <summary>Counts by target user.</summary>
    public IReadOnlyDictionary<string, long> ByTargetUser { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by target domain.</summary>
    public IReadOnlyDictionary<string, long> ByTargetDomain { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by logon type.</summary>
    public IReadOnlyDictionary<string, long> ByLogonType { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by IP address.</summary>
    public IReadOnlyDictionary<string, long> ByIpAddress { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by workstation name.</summary>
    public IReadOnlyDictionary<string, long> ByWorkstationName { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by computer name the event was logged on.</summary>
    public IReadOnlyDictionary<string, long> ByComputerName { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional event samples (small, capped).</summary>
    public IReadOnlyList<SecurityUserLogonSample> Samples { get; set; } = Array.Empty<SecurityUserLogonSample>();
}

/// <summary>
/// Sample row for a single logon-related event.
/// </summary>
internal sealed class SecurityUserLogonSample {
    /// <summary>Event time (UTC), or null when the source record has no timestamp.</summary>
    public DateTime? TimeCreatedUtc { get; set; }
    /// <summary>Event ID.</summary>
    public int Id { get; set; }
    /// <summary>Computer name the event was logged on.</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>Target user name.</summary>
    public string TargetUser { get; set; } = string.Empty;
    /// <summary>Target domain name.</summary>
    public string TargetDomain { get; set; } = string.Empty;
    /// <summary>Logon type value (string).</summary>
    public string LogonType { get; set; } = string.Empty;
    /// <summary>IP address (may be empty).</summary>
    public string IpAddress { get; set; } = string.Empty;
    /// <summary>Workstation name (may be empty).</summary>
    public string WorkstationName { get; set; } = string.Empty;

    /// <summary>Subject user name (may be empty).</summary>
    public string SubjectUser { get; set; } = string.Empty;
}
