using System.Globalization;

namespace EventViewerX;

/// <summary>Creates bounded context facts only from fields carried by Group Policy audit events.</summary>
public static class GroupPolicyContextFactFactory {
    /// <summary>Creates a fact for a Group Policy object event, or null for scope and WMI-filter events.</summary>
    public static EventContextFact? Create(GroupPolicyAuditRecord record) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        if (record.TargetKind != GroupPolicyAuditTargetKind.GroupPolicyObject) {
            return null;
        }
        string? canonicalId = record.GroupPolicyId?.ToString("D");
        if (string.IsNullOrWhiteSpace(canonicalId)) {
            return null;
        }
        string[] aliases = new[] {
                record.ObjectDistinguishedName,
                record.OldObjectDistinguishedName,
                record.NewObjectDistinguishedName,
                record.ObjectGuid?.ToString("D") ?? string.Empty
            }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string? displayName = string.Equals(record.AttributeName, "displayName", StringComparison.OrdinalIgnoreCase) &&
                              IsAddedValue(record.OperationType)
            ? NormalizeAttributeValue(record.AttributeValue)
            : null;
        return new EventContextFact {
            ObjectKind = EventContextObjectKind.GroupPolicy,
            CanonicalId = canonicalId!,
            Aliases = aliases,
            DisplayName = displayName,
            Domain = ExtractDomain(record.ObjectDistinguishedName),
            DistinguishedName = string.IsNullOrWhiteSpace(record.ObjectDistinguishedName)
                ? null
                : record.ObjectDistinguishedName,
            EffectiveAtUtc = record.TimeCreatedUtc,
            ObservedAtUtc = DateTime.UtcNow,
            IsDeleted = record.Kind == GroupPolicyAuditEventKind.Deleted,
            Provenance = EventContextProvenance.Event,
            SourceIdentity = CreateSourceIdentity(record),
            ProviderName = "EventViewerX.GroupPolicyAudit",
            ProviderSchemaVersion = 1,
            ConfidenceReason = "The fact was carried by a selected Security 5136/5137/5139/5141 event.",
            IsShareable = true
        };
    }

    private static string CreateSourceIdentity(GroupPolicyAuditRecord record) => string.Join("|", new[] {
        record.SourceComputer,
        record.OriginalLogName,
        record.EventId.ToString(CultureInfo.InvariantCulture),
        record.RecordId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        record.TimeCreatedUtc.ToString("O", CultureInfo.InvariantCulture),
        record.OperationCorrelationId
    });

    private static string? NormalizeAttributeValue(string value) {
        string candidate = value?.Trim() ?? string.Empty;
        return candidate.Length == 0 || candidate == "-" ? null : candidate;
    }

    private static bool IsAddedValue(string operationType) =>
        string.Equals(operationType, "%%14674", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(operationType, "Value Added", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractDomain(string distinguishedName) {
        string[] components = (distinguishedName ?? string.Empty)
            .Split(',')
            .Select(static value => value.Trim())
            .Where(static value => value.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
            .Select(static value => value.Substring(3))
            .Where(static value => value.Length > 0)
            .ToArray();
        return components.Length == 0 ? null : string.Join(".", components);
    }
}
