using EventViewerX.Native;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    /// <summary>Reads stored event rows as canonical detection observations without losing evidence identity.</summary>
    public async Task<EventStoreObservationReadResult> ReadObservationsAsync(
        EventStoreQuery query,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventReport report = await ReadReportAsync(query, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var observations = new List<EventObservation>(report.Rows.Count);
        bool legacyIdentity = false;
        foreach (EventReportRow row in report.Rows) {
            cancellationToken.ThrowIfCancellationRequested();
            EventObject source = RestoreSource(row);
            string identity = row.ObservationIdentity;
            if (string.IsNullOrWhiteSpace(identity)) {
                identity = EventCheckpointBoundaryIdentity.Create(source);
                legacyIdentity = true;
            }
            DateTime received = (row.ReceivedTimeUtc ?? row.StoredTimeUtc ?? row.TimeCreated).ToUniversalTime();
            DateTime processed = (row.ProcessedTimeUtc ?? row.StoredTimeUtc ?? received).ToUniversalTime();
            if (processed < received) {
                throw new InvalidDataException(
                    $"Stored observation '{identity}' has a processed time earlier than its received time.");
            }
            observations.Add(EventObservation.Restore(
                source,
                identity,
                string.IsNullOrWhiteSpace(row.Type) ? "Generic" : row.Type,
                row.Values,
                received,
                processed));
        }
        string? diagnostic = report.CompletenessDiagnostic;
        if (legacyIdentity) {
            const string legacy = "One or more legacy rows predate durable observation identities; deterministic fallback identities were reconstructed from retained metadata.";
            diagnostic = string.IsNullOrWhiteSpace(diagnostic) ? legacy : diagnostic + " " + legacy;
        }
        return new EventStoreObservationReadResult(
            observations,
            report.EventsScanned,
            report.ScanLimitReached,
            diagnostic);
    }

    /// <summary>
    /// Evaluates a stored historical window and automatically loads the preceding stateful rule window so
    /// threshold and temporal correlation survive process restarts.
    /// </summary>
    public async Task<EventDetectionExecutionResult> EvaluateDetectionAsync(
        EventStoreQuery query,
        EventDetectionPlan plan,
        EventDetectionEngineOptions? options = null,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }
        EventStoreQuery historical = query.Snapshot();
        DateTime? resultStart = historical.StartTime;
        if (resultStart.HasValue && plan.MaximumStatefulWindow > TimeSpan.Zero) {
            historical.StartTime = resultStart.Value - plan.MaximumStatefulWindow;
        }
        historical.Oldest = true;
        ApplySafeDetectionSelectors(historical, plan);
        EventStoreObservationReadResult read = await ReadObservationsAsync(historical, cancellationToken)
            .ConfigureAwait(false);
        EventDetectionCoverage coverage = options?.Coverage?.Snapshot() ?? EventDetectionCoverage.Unknown();
        if (!read.IsComplete) {
            coverage = coverage.WithFailures(new[] {
                read.CompletenessDiagnostic ?? "The stored historical candidate window was not exhaustive."
            });
        }
        var engineOptions = new EventDetectionEngineOptions {
            MaximumObservations = options?.MaximumObservations ?? 1_000_000,
            MaximumGroups = options?.MaximumGroups ?? 25_000,
            MaximumStateObservations = options?.MaximumStateObservations ?? 250_000,
            MaximumStateBytes = options?.MaximumStateBytes ?? 256L * 1024L * 1024L,
            MaximumCandidateRules = options?.MaximumCandidateRules ?? 10_000,
            Coverage = coverage
        };
        EventDetectionExecutionResult evaluated = EventDetectionEngine.Evaluate(read.Observations, plan, engineOptions);
        EventDetectionFinding[] selected = resultStart.HasValue
            ? evaluated.Findings.Where(finding => finding.EndTimeUtc >= resultStart.Value).ToArray()
            : evaluated.Findings.ToArray();
        return new EventDetectionExecutionResult(evaluated.Observations, selected, evaluated.Coverage);
    }

    private static void ApplySafeDetectionSelectors(EventStoreQuery query, EventDetectionPlan plan) {
        if (query.EventIds is { Count: > 0 }) {
            return;
        }
        int[][] perRule = plan.Rules.Select(rule => rule.EventIds
                .Concat(rule.Steps.SelectMany(static step => step.EventIds))
                .Concat(EventTypeCatalog.Expand(rule.EventTypes
                        .Concat(rule.Steps.SelectMany(static step => step.EventTypes)))
                    .SelectMany(static type => EventTypeCatalog.GetSources(new[] { type }))
                    .SelectMany(static source => source.EventIds))
                .Distinct()
                .ToArray())
            .ToArray();
        if (perRule.Length > 0 && perRule.All(static ids => ids.Length > 0)) {
            query.EventIds = perRule.SelectMany(static ids => ids).Distinct().ToArray();
        }
    }

    private static EventObject RestoreSource(EventReportRow row) {
        var metadata = new NativeEventMetadata(
            row.Provider ?? string.Empty,
            providerId: null,
            row.EventId,
            qualifiers: null,
            row.LevelValue,
            task: null,
            opcode: null,
            keywords: null,
            row.TimeCreated.ToUniversalTime(),
            row.RecordId,
            row.ActivityId,
            row.RelatedActivityId,
            processId: null,
            threadId: null,
            row.SourceLog ?? string.Empty,
            row.SourceComputer ?? string.Empty,
            userId: null,
            version: null);
        var message = new NativeEventMessage(
            metadata,
            row.Message ?? string.Empty,
            row.Level ?? string.Empty,
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            bookmark: null,
            cultureName: string.Empty,
            string.IsNullOrWhiteSpace(row.Message)
                ? EventMessageRenderStatus.NotRequested
                : EventMessageRenderStatus.Rendered,
            renderErrorCode: 0);
        var source = new EventObject(
            message,
            row.CollectorComputer ?? string.Empty,
            row.ContainerLog ?? row.SourceLog ?? string.Empty) {
            QuerySourceKind = row.SourceKind
        };
        foreach (KeyValuePair<string, object?> value in row.Values) {
            source.Data[value.Key] = FormatStoredValue(value.Value);
        }
        return source;
    }

    private static string FormatStoredValue(object? value) {
        if (value == null) {
            return string.Empty;
        }
        if (value is DateTime dateTime) {
            return dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
        if (value is DateTimeOffset offset) {
            return offset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
