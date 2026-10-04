using System.Globalization;
using System.Security.Cryptography;

namespace EventViewerX.Reporting;

/// <summary>Creates a detached data-minimized snapshot for existing report renderers and transports.</summary>
public static class EventReportPrivacy {
    /// <summary>Omits unselected payloads, messages, normalization evidence, source details and descriptive text without modifying the input.</summary>
    /// <remarks>Protect and retain the supplied key outside the export when stable pseudonyms are needed. Neither the key nor raw evidence is added to the result.</remarks>
    public static EventReport Apply(EventReport report, EventReportPrivacyOptions? options = null,
        byte[]? pseudonymizationKey = null, CancellationToken cancellationToken = default) {
        if (report == null) { throw new ArgumentNullException(nameof(report)); }
        options ??= new EventReportPrivacyOptions();
        HashSet<string> retained = ValidateFields(options.RetainedValueFields, nameof(options.RetainedValueFields));
        HashSet<string> pseudonymized = ValidateFields(options.PseudonymizedValueFields, nameof(options.PseudonymizedValueFields));
        if (retained.Overlaps(pseudonymized)) {
            throw new ArgumentException("A payload field cannot be both retained and pseudonymized.", nameof(options));
        }
        bool needsKey = options.PseudonymizeSourceIdentities || pseudonymized.Count > 0;
        if (needsKey && (pseudonymizationKey == null || pseudonymizationKey.Length < 32)) {
            throw new ArgumentException("Pseudonymization requires a key containing at least 32 bytes.", nameof(pseudonymizationKey));
        }
        using HMACSHA256? hmac = needsKey ? new HMACSHA256(pseudonymizationKey!) : null;
        var rows = new Dictionary<EventReportRow, EventReportRow>();
        foreach (EventReportRow source in report.Rows) {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object?> item in source.Values) {
                if (retained.Contains(item.Key)) {
                    values[item.Key] = CopyScalar(item.Value, item.Key);
                } else if (pseudonymized.Contains(item.Key)) {
                    values[item.Key] = Token(hmac!, item.Key, CopyScalar(item.Value, item.Key));
                }
            }
            rows[source] = new EventReportRow {
                TimeCreated = source.TimeCreated, ReceivedTimeUtc = source.ReceivedTimeUtc,
                ProcessedTimeUtc = source.ProcessedTimeUtc, StoredTimeUtc = source.StoredTimeUtc,
                EventId = source.EventId, RecordId = source.RecordId, Type = "Generic",
                Provider = source.Provider, SourceLog = source.SourceLog, SourceKind = source.SourceKind,
                Level = source.Level, LevelValue = source.LevelValue, ProcessId = source.ProcessId, ThreadId = source.ThreadId,
                SourceComputer = Identity(nameof(source.SourceComputer), source.SourceComputer),
                CollectorComputer = Identity(nameof(source.CollectorComputer), source.CollectorComputer),
                ContainerLog = Identity(nameof(source.ContainerLog), source.ContainerLog),
                ObservationIdentity = Identity(nameof(source.ObservationIdentity), source.ObservationIdentity),
                Values = values
            };
        }
        EventReportRow[] detachedRows = report.Rows.Select(row => rows[row]).ToArray();
        // One standard generic schema removes custom type labels, aliases and descriptions,
        // and remains compatible with the existing storage and snapshot rehydration contract.
        EventReportSection[] sections = {
            new("Generic", "Events", string.Empty, EventReportSectionKind.Generic,
                EventReportTableProjection.BuildGenericColumns(detachedRows), detachedRows)
        };
        EventReportCoverage[] coverage = report.Coverage.Select(source => new EventReportCoverage {
            MachineName = Identity("Coverage.MachineName", source.MachineName),
            LogName = Identity("Coverage.LogName", source.LogName),
            Succeeded = source.Succeeded, Status = source.Succeeded ? "Succeeded" : "Failed"
        }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new EventReport("EventViewerX export", report.GeneratedAt, report.QueryDuration,
            detachedRows, sections, coverage, report.EventsScanned,
            report.ScanLimitReached, string.IsNullOrWhiteSpace(report.CompletenessDiagnostic) ? null
                : "Input is incomplete; source detail is omitted by the export policy.");

        string Identity(string field, string value) => options.PseudonymizeSourceIdentities && value.Length > 0
            ? Token(hmac!, field, value)! : string.Empty;
    }

    private static HashSet<string> ValidateFields(IEnumerable<string>? fields, string parameter) {
        if (fields == null) { throw new ArgumentNullException(parameter); }
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string field in fields) {
            if (string.IsNullOrWhiteSpace(field) || EventReportRow.IsCommonFieldName(field) ||
                field.Equals(EventDefinition.OutputMetadataFieldName, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("Select payload field names rather than common or normalization metadata fields.", parameter);
            }
            result.Add(field);
        }
        return result;
    }

    private static object? CopyScalar(object? value, string field) {
        if (value == null || value is string || value is bool || value is char || value is DateTime || value is DateTimeOffset ||
            value is Guid || value is TimeSpan || value is byte || value is sbyte || value is short || value is ushort ||
            value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal ||
            value is Enum) {
            return value;
        }
        throw new ArgumentException($"Selected field '{field}' must contain a scalar value; nested objects require an explicit projection.");
    }

    private static string? Token(HMACSHA256 hmac, string field, object? value) {
        if (value == null) { return null; }
        string text = value is DateTime date ? date.ToString("O", CultureInfo.InvariantCulture)
            : value is DateTimeOffset offset ? offset.ToString("O", CultureInfo.InvariantCulture)
            // General numeric formatting differs between .NET Framework and modern .NET,
            // and can collapse distinct floating-point values. Encode their exact IEEE bits.
            : value is double number ? BitConverter.DoubleToInt64Bits(number).ToString("X16", CultureInfo.InvariantCulture)
            : value is float single ? BitConverter.ToInt32(BitConverter.GetBytes(single), 0).ToString("X8", CultureInfo.InvariantCulture)
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(field.ToLowerInvariant() + "\0" + value.GetType().FullName + "\0" + text));
        return "hmac-sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
