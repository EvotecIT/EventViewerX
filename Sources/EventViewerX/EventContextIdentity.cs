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
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true)) {
            writer.Write(3);
            WriteField(writer, ((int)snapshot.ObjectKind).ToString(CultureInfo.InvariantCulture));
            WriteField(writer, snapshot.CanonicalId);
            string[] aliases = snapshot.Aliases
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            writer.Write(aliases.Length);
            foreach (string alias in aliases) {
                WriteField(writer, alias);
            }
            WriteField(writer, snapshot.DisplayName);
            writer.Write(snapshot.DisplayNameObserved);
            WriteField(writer, snapshot.Domain);
            WriteField(writer, snapshot.DistinguishedName);
            WriteField(writer, snapshot.EffectiveAtUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.Write(snapshot.IsDeleted);
            WriteField(writer, ((int)snapshot.Provenance).ToString(CultureInfo.InvariantCulture));
            WriteField(writer, snapshot.SourceIdentity);
            WriteField(writer, snapshot.ProviderName);
            WriteField(writer, snapshot.ProviderSchemaVersion.ToString(CultureInfo.InvariantCulture));
            WriteField(writer, snapshot.AuthorizationContext);
            writer.Write(snapshot.IsShareable);
        }
        using SHA256 sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(payload.ToArray())).Replace("-", string.Empty);
    }

    private static void WriteField(BinaryWriter writer, string? value) {
        if (value == null) {
            writer.Write(-1);
            return;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
