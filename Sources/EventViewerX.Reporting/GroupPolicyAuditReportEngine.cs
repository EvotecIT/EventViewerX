using System.Diagnostics;

namespace EventViewerX.Reporting;

/// <summary>Creates normal EventViewerX reports from persistent Group Policy audit context.</summary>
public static class GroupPolicyAuditReportEngine {
    /// <summary>Queries Group Policy audit events, updates context, and creates one reusable report snapshot.</summary>
    public static async Task<EventReport> QueryAsync(
        GroupPolicyAuditQuery query,
        string? title = null,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        var timer = Stopwatch.StartNew();
        var execution = new GroupPolicyAuditQueryExecutionInfo();
        var projections = new List<EventReportProjection>();
        await foreach (GroupPolicyAuditRecord record in GroupPolicyAuditEngine.ReadAsync(
                           query,
                           execution,
                           cancellationToken)) {
            projections.Add(EventReportProjectionFactory.Create(record));
        }
        timer.Stop();
        EventReportRow[] rows = projections.Select(static projection => projection.Row).ToArray();
        EventReportSectionDefinition emptySection = EventReportProjectionFactory.CreateGroupPolicyAuditDefinition();
        EventReportCoverage[] coverage = BuildCoverage(query, execution.TargetFailures);
        return new EventReport(
            string.IsNullOrWhiteSpace(title) ? "Group Policy audit" : title!.Trim(),
            DateTime.UtcNow,
            timer.Elapsed,
            rows,
            EventReportSectionBuilder.Build(
                projections,
                projections.Count == 0 ? new[] { emptySection } : null),
            coverage,
            execution.EventsScanned,
            execution.IsTruncated,
            EventCompletenessDiagnostic.Compose(
                execution.ScanLimitReached ? "The Group Policy candidate scan limit was reached" : null,
                execution.ResultLimitReached
                    ? $"The Group Policy result limit MaxEvents {query.MaxEvents:N0} was reached; additional matching events exist"
                    : null));
    }

    internal static EventReportCoverage[] BuildCoverage(
        GroupPolicyAuditQuery query,
        IReadOnlyList<EventLogQueryTargetFailure> failures) {

        if (query.Paths != null && query.Paths.Count > 0) {
            return query.Paths.Select(path => {
                string fullPath = Path.GetFullPath(path);
                EventLogQueryTargetFailure? failure = failures.FirstOrDefault(item =>
                    string.Equals(Path.GetFullPath(item.LogName), fullPath, StringComparison.OrdinalIgnoreCase));
                return new EventReportCoverage {
                    MachineName = "Offline",
                    LogName = fullPath,
                    Succeeded = failure == null,
                    Status = failure?.Kind.ToString() ?? "Succeeded",
                    Detail = failure?.Message ?? string.Empty
                };
            }).ToArray();
        }
        IReadOnlyList<string?> targets = query.MachineNames ?? new string?[] { null };
        string queriedLog = string.IsNullOrWhiteSpace(query.CollectorLogName)
            ? "Security"
            : query.CollectorLogName!;
        return targets.Select(target => {
            string machine = string.IsNullOrWhiteSpace(target) ? Environment.MachineName : target!;
            EventLogQueryTargetFailure? failure = failures.FirstOrDefault(item =>
                string.Equals(item.MachineName, machine, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.LogName, queriedLog, StringComparison.OrdinalIgnoreCase));
            return new EventReportCoverage {
                MachineName = machine,
                LogName = queriedLog,
                Succeeded = failure == null,
                Status = failure?.Kind.ToString() ?? "Succeeded",
                Detail = failure?.Message ?? string.Empty
            };
        }).ToArray();
    }
}
