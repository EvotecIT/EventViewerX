using System;
using System.Collections.Generic;
using EventViewerX.Reports;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Common EVTX security report request.
/// </summary>
public sealed class SecurityEvtxQueryRequest {
    /// <summary>
    /// EVTX file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional UTC lower bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Optional UTC upper bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Maximum number of scanned events.
    /// </summary>
    public int MaxEventsScanned { get; set; }

    /// <summary>
    /// Number of top rows to include per dimension.
    /// </summary>
    public int Top { get; set; } = 20;

    /// <summary>
    /// When true, include sample events in output.
    /// </summary>
    public bool IncludeSamples { get; set; }

    /// <summary>
    /// Sample size cap when <see cref="IncludeSamples"/> is enabled.
    /// </summary>
    public int SampleSize { get; set; } = 20;
}

/// <summary>
/// EVTX user-logons report query result.
/// </summary>
public sealed class SecurityUserLogonsQueryResult {
    /// <summary>
    /// Queried EVTX path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Security provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Included event IDs.
    /// </summary>
    public IReadOnlyList<int> EventIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Maximum scanned events cap used by query.
    /// </summary>
    public int MaxEventsScanned { get; set; }

    /// <summary>
    /// Number of scanned events.
    /// </summary>
    public int ScannedEvents { get; set; }

    /// <summary>
    /// Number of matched events.
    /// </summary>
    public int MatchedEvents { get; set; }

    /// <summary>
    /// Indicates whether scanning stopped at cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Minimum matched event time (UTC).
    /// </summary>
    public DateTime? TimeCreatedUtcMin { get; set; }

    /// <summary>
    /// Maximum matched event time (UTC).
    /// </summary>
    public DateTime? TimeCreatedUtcMax { get; set; }

    /// <summary>
    /// Number of top rows requested per dimension.
    /// </summary>
    public int Top { get; set; }

    /// <summary>
    /// Top rows by event ID.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByEventId { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by target user.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByTargetUser { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by target domain.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByTargetDomain { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by logon type.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByLogonType { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by IP address.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByIpAddress { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by workstation name.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByWorkstationName { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by computer name.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByComputerName { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Indicates whether sample rows are included.
    /// </summary>
    public bool IncludeSamples { get; set; }

    /// <summary>
    /// Effective sample size when samples are included.
    /// </summary>
    public int? SampleSize { get; set; }

    /// <summary>
    /// Optional sample rows.
    /// </summary>
    public IReadOnlyList<SecurityUserLogonSample>? Samples { get; set; }

    /// <summary>
    /// Effective query start bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Effective query end bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }
}

/// <summary>
/// EVTX failed-logons report query result.
/// </summary>
public sealed class SecurityFailedLogonsQueryResult {
    /// <summary>
    /// Queried EVTX path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Security provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Included event ID.
    /// </summary>
    public int EventId { get; set; }

    /// <summary>
    /// Maximum scanned events cap used by query.
    /// </summary>
    public int MaxEventsScanned { get; set; }

    /// <summary>
    /// Number of scanned events.
    /// </summary>
    public int ScannedEvents { get; set; }

    /// <summary>
    /// Number of matched events.
    /// </summary>
    public int MatchedEvents { get; set; }

    /// <summary>
    /// Indicates whether scanning stopped at cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Minimum matched event time (UTC).
    /// </summary>
    public DateTime? TimeCreatedUtcMin { get; set; }

    /// <summary>
    /// Maximum matched event time (UTC).
    /// </summary>
    public DateTime? TimeCreatedUtcMax { get; set; }

    /// <summary>
    /// Number of top rows requested per dimension.
    /// </summary>
    public int Top { get; set; }

    /// <summary>
    /// Top rows by target user.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByTargetUser { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by target domain.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByTargetDomain { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by logon type.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByLogonType { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by IP address.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByIpAddress { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by workstation name.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByWorkstationName { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by computer name.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByComputerName { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by status code.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByStatus { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by status name.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByStatusName { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by sub-status code.
    /// </summary>
    public IReadOnlyList<ReportTopRow> BySubStatus { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by sub-status name.
    /// </summary>
    public IReadOnlyList<ReportTopRow> BySubStatusName { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by failure reason.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByFailureReason { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Indicates whether sample rows are included.
    /// </summary>
    public bool IncludeSamples { get; set; }

    /// <summary>
    /// Effective sample size when samples are included.
    /// </summary>
    public int? SampleSize { get; set; }

    /// <summary>
    /// Optional sample rows.
    /// </summary>
    public IReadOnlyList<SecurityFailedLogonSample>? Samples { get; set; }

    /// <summary>
    /// Effective query start bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Effective query end bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }
}

/// <summary>
/// EVTX account-lockouts report query result.
/// </summary>
public sealed class SecurityAccountLockoutsQueryResult {
    /// <summary>
    /// Queried EVTX path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Security provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Included event ID.
    /// </summary>
    public int EventId { get; set; }

    /// <summary>
    /// Maximum scanned events cap used by query.
    /// </summary>
    public int MaxEventsScanned { get; set; }

    /// <summary>
    /// Number of scanned events.
    /// </summary>
    public int ScannedEvents { get; set; }

    /// <summary>
    /// Number of matched events.
    /// </summary>
    public int MatchedEvents { get; set; }

    /// <summary>
    /// Indicates whether scanning stopped at cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Minimum matched event time (UTC).
    /// </summary>
    public DateTime? TimeCreatedUtcMin { get; set; }

    /// <summary>
    /// Maximum matched event time (UTC).
    /// </summary>
    public DateTime? TimeCreatedUtcMax { get; set; }

    /// <summary>
    /// Number of top rows requested per dimension.
    /// </summary>
    public int Top { get; set; }

    /// <summary>
    /// Top rows by target user.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByTargetUser { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by target domain.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByTargetDomain { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by caller computer.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByCallerComputerName { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by subject user.
    /// </summary>
    public IReadOnlyList<ReportTopRow> BySubjectUser { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Top rows by computer name.
    /// </summary>
    public IReadOnlyList<ReportTopRow> ByComputerName { get; set; } = Array.Empty<ReportTopRow>();

    /// <summary>
    /// Indicates whether sample rows are included.
    /// </summary>
    public bool IncludeSamples { get; set; }

    /// <summary>
    /// Effective sample size when samples are included.
    /// </summary>
    public int? SampleSize { get; set; }

    /// <summary>
    /// Optional sample rows.
    /// </summary>
    public IReadOnlyList<SecurityAccountLockoutSample>? Samples { get; set; }

    /// <summary>
    /// Effective query start bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Effective query end bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }
}
