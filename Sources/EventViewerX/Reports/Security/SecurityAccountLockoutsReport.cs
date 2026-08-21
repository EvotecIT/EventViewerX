using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Summary report for Windows Security account lockout events (4740).
/// </summary>
internal sealed class SecurityAccountLockoutsReport {
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

    /// <summary>Counts by target user.</summary>
    public IReadOnlyDictionary<string, long> ByTargetUser { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by target domain.</summary>
    public IReadOnlyDictionary<string, long> ByTargetDomain { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by caller computer name (if available).</summary>
    public IReadOnlyDictionary<string, long> ByCallerComputerName { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by subject user (the account that performed the action, when present).</summary>
    public IReadOnlyDictionary<string, long> BySubjectUser { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Counts by computer name the event was logged on.</summary>
    public IReadOnlyDictionary<string, long> ByComputerName { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional event samples (small, capped).</summary>
    public IReadOnlyList<SecurityAccountLockoutSample> Samples { get; set; } = Array.Empty<SecurityAccountLockoutSample>();
}

/// <summary>
/// Sample row for a single 4740 event.
/// </summary>
internal sealed class SecurityAccountLockoutSample {
    /// <summary>Event time (UTC), or null when the source record has no timestamp.</summary>
    public DateTime? TimeCreatedUtc { get; set; }
    /// <summary>Event ID (typically 4740).</summary>
    public int Id { get; set; }
    /// <summary>Computer name the event was logged on.</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>Target user name.</summary>
    public string TargetUser { get; set; } = string.Empty;
    /// <summary>Target domain name.</summary>
    public string TargetDomain { get; set; } = string.Empty;
    /// <summary>Caller computer name (may be empty).</summary>
    public string CallerComputerName { get; set; } = string.Empty;
    /// <summary>Subject user name (may be empty).</summary>
    public string SubjectUser { get; set; } = string.Empty;
}
