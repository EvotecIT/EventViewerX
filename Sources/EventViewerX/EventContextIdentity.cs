using System.Globalization;
using System.Security.Cryptography;

namespace EventViewerX;

/// <summary>Canonical identity and deterministic fact-key helpers shared by context stores.</summary>
public static class EventContextIdentity {
    /// <summary>Normalizes a canonical identity for one object family.</summary>
    public static string NormalizeCanonicalId(EventContextObjectKind objectKind, string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("Canonical identity cannot be empty.", nameof(value));
        }
        string candidate = value.Trim().Trim('{', '}');
        if (objectKind == EventContextObjectKind.GroupPolicy && Guid.TryParse(candidate, out Guid identifier)) {
            return identifier.ToString("D").ToUpperInvariant();
        }
        return candidate.ToUpperInvariant();
    }

    /// <summary>Normalizes an alternate identity for exact, case-insensitive matching.</summary>
    public static string NormalizeAlias(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("Alias cannot be empty.", nameof(value));
        }
        string candidate = value.Trim().Trim('{', '}');
        return Guid.TryParse(candidate, out Guid identifier)
            ? identifier.ToString("D").ToUpperInvariant()
            : candidate.ToUpperInvariant();
    }

    /// <summary>Creates a stable key for idempotent fact storage.</summary>
    public static string CreateFactKey(EventContextFact fact) {
        EventContextFact snapshot = EventContextResolver.ValidateAndSnapshot(fact);
        string payload = string.Join("\n", new[] {
            ((int)snapshot.ObjectKind).ToString(CultureInfo.InvariantCulture),
            snapshot.CanonicalId,
            string.Join("|", snapshot.Aliases.OrderBy(static value => value, StringComparer.Ordinal)),
            snapshot.EffectiveAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ((int)snapshot.Provenance).ToString(CultureInfo.InvariantCulture),
            snapshot.SourceIdentity,
            snapshot.ProviderName,
            snapshot.ProviderSchemaVersion.ToString(CultureInfo.InvariantCulture)
        });
        using SHA256 sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", string.Empty);
    }
}
