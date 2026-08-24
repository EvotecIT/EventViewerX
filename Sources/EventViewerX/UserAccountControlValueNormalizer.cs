using System.Globalization;

namespace EventViewerX;

internal sealed class UserAccountControlValueNormalizer : IEventValueNormalizer {
    private static readonly KeyValuePair<long, string>[] Flags = {
        new(0x0001, "SCRIPT"), new(0x0002, "ACCOUNTDISABLE"),
        new(0x0008, "HOMEDIR_REQUIRED"), new(0x0010, "LOCKOUT"),
        new(0x0020, "PASSWD_NOTREQD"), new(0x0040, "PASSWD_CANT_CHANGE"),
        new(0x0080, "ENCRYPTED_TEXT_PWD_ALLOWED"), new(0x0100, "TEMP_DUPLICATE_ACCOUNT"),
        new(0x0200, "NORMAL_ACCOUNT"), new(0x0800, "INTERDOMAIN_TRUST_ACCOUNT"),
        new(0x1000, "WORKSTATION_TRUST_ACCOUNT"), new(0x2000, "SERVER_TRUST_ACCOUNT"),
        new(0x10000, "DONT_EXPIRE_PASSWORD"), new(0x20000, "MNS_LOGON_ACCOUNT"),
        new(0x40000, "SMARTCARD_REQUIRED"), new(0x80000, "TRUSTED_FOR_DELEGATION"),
        new(0x100000, "NOT_DELEGATED"), new(0x200000, "USE_DES_KEY_ONLY"),
        new(0x400000, "DONT_REQ_PREAUTH"), new(0x800000, "PASSWORD_EXPIRED"),
        new(0x1000000, "TRUSTED_TO_AUTH_FOR_DELEGATION"),
        new(0x04000000, "PARTIAL_SECRETS_ACCOUNT")
    };

    public string Name => "user-account-control";

    public int Version => 2;

    public bool CanNormalize(EventValueContext context) =>
        context.FieldName.IndexOf("UserAccountControl", StringComparison.OrdinalIgnoreCase) >= 0 ||
        string.Equals(context.FieldName, "UacValue", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(context.FieldName, "OldUacValue", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(context.FieldName, "NewUacValue", StringComparison.OrdinalIgnoreCase);

    public EventNormalizedValue Normalize(EventValueContext context) {
        string raw = EventValueNormalizer.Format(context.RawValue).Trim();
        if (raw.Length == 0) {
            return EventValueNormalizer.Unchanged(context);
        }
        if (!TryParse(raw, out long mask)) {
            if (raw.IndexOf(',') >= 0 || Flags.Any(flag => raw.IndexOf(flag.Value, StringComparison.OrdinalIgnoreCase) >= 0)) {
                string[] names = raw.Split(',')
                    .Select(static value => value.Trim())
                    .Where(static value => value.Length > 0)
                    .Select(static value => value.ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToArray();
                return EventValueNormalizer.Create(
                    context,
                    names,
                    string.Join(", ", names),
                    EventNormalizedValueKind.FlagSet,
                    Name,
                    Version,
                    EventNormalizationOutcome.Normalized);
            }
            return EventValueNormalizer.Create(
                context,
                context.RawValue,
                raw,
                EventNormalizedValueKind.FlagSet,
                Name,
                Version,
                EventNormalizationOutcome.Malformed,
                warnings: new[] { $"UserAccountControl value '{raw}' is not a supported decimal or hexadecimal mask." });
        }
        string[] selected = Flags.Where(flag => (mask & flag.Key) != 0)
            .Select(static flag => flag.Value)
            .ToArray();
        long knownMask = Flags.Aggregate(0L, static (value, flag) => value | flag.Key);
        long unknownMask = mask & ~knownMask;
        var canonical = new List<string>(selected);
        if (unknownMask != 0) {
            canonical.Add("UNKNOWN_0x" + unknownMask.ToString("X", CultureInfo.InvariantCulture));
        }
        canonical.Sort(StringComparer.Ordinal);
        return EventValueNormalizer.Create(
            context,
            canonical.ToArray(),
            canonical.Count == 0 ? "NONE" : string.Join(", ", canonical),
            EventNormalizedValueKind.FlagSet,
            Name,
            Version,
            isLossless: unknownMask == 0,
            warnings: unknownMask == 0
                ? Array.Empty<string>()
                : new[] { $"Unknown UserAccountControl bits 0x{unknownMask:X} were retained in the canonical and display values." });
    }

    private static bool TryParse(string value, out long result) {
        string candidate = value;
        NumberStyles style = NumberStyles.Integer;
        if (candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            candidate = candidate.Substring(2);
            style = NumberStyles.AllowHexSpecifier;
        }
        return long.TryParse(candidate, style, CultureInfo.InvariantCulture, out result);
    }
}
