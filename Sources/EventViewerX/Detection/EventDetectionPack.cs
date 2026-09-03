using System.Security.Cryptography;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EventViewerX;

/// <summary>Immutable, versioned, integrity-protected collection of native or imported detections.</summary>
public sealed class EventDetectionPack {
    /// <summary>Detection-engine contract version implemented by this EventViewerX release.</summary>
    public const string CurrentEngineVersion = "4.0.0";

    /// <summary>Canonical observation schema version implemented by this EventViewerX release.</summary>
    public const string CurrentObservationSchemaVersion = "1.0.0";

    private const string RsaSha256 = "RSA-SHA256";
    private static readonly Regex SemanticVersion = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly EventDetectionRuleDefinition[] _rules;

    private EventDetectionPack(
        string packId,
        string version,
        string minimumEngineVersion,
        string observationSchemaVersion,
        IReadOnlyList<string> authors,
        string license,
        DateTime createdUtc,
        EventDetectionRuleDefinition[] rules,
        string contentHash,
        string signatureAlgorithm,
        string signature) {

        PackId = packId;
        Version = version;
        MinimumEngineVersion = minimumEngineVersion;
        ObservationSchemaVersion = observationSchemaVersion;
        Authors = Array.AsReadOnly(authors.ToArray());
        License = license;
        CreatedUtc = createdUtc;
        _rules = rules;
        ContentHash = contentHash;
        SignatureAlgorithm = signatureAlgorithm;
        Signature = signature;
    }

    /// <summary>Stable pack identifier.</summary>
    public string PackId { get; }
    /// <summary>Semantic pack version.</summary>
    public string Version { get; }
    /// <summary>Minimum compatible EventViewerX engine version.</summary>
    public string MinimumEngineVersion { get; }
    /// <summary>Required canonical observation schema version.</summary>
    public string ObservationSchemaVersion { get; }
    /// <summary>Content authors.</summary>
    public IReadOnlyList<string> Authors { get; }
    /// <summary>Pack content license.</summary>
    public string License { get; }
    /// <summary>UTC pack creation time.</summary>
    public DateTime CreatedUtc { get; }
    /// <summary>SHA-256 hash of the canonical pack payload.</summary>
    public string ContentHash { get; }
    /// <summary>Optional signature algorithm.</summary>
    public string SignatureAlgorithm { get; }
    /// <summary>Optional Base64 signature over the content hash.</summary>
    public string Signature { get; }
    /// <summary>Detached rule definitions.</summary>
    public IReadOnlyList<EventDetectionRuleDefinition> Rules =>
        Array.AsReadOnly(_rules.Select(static rule => rule.Snapshot()).ToArray());

    /// <summary>Creates an unsigned pack and computes deterministic rule and pack hashes.</summary>
    public static EventDetectionPack Create(
        string packId,
        string version,
        IEnumerable<EventDetectionRuleDefinition> rules,
        IEnumerable<string>? authors = null,
        string? license = null,
        string minimumEngineVersion = "4.0.0",
        string observationSchemaVersion = "1.0.0",
        DateTime? createdUtc = null) {

        string normalizedPackId = NormalizeRequired(packId, nameof(packId), 200);
        string normalizedVersion = NormalizeSemanticVersion(version, nameof(version));
        string normalizedMinimum = NormalizeSemanticVersion(minimumEngineVersion, nameof(minimumEngineVersion));
        string normalizedSchema = NormalizeSemanticVersion(observationSchemaVersion, nameof(observationSchemaVersion));
        string normalizedLicense = NormalizeOptional(license, 200);
        string[] normalizedAuthors = NormalizeAuthors(authors);
        EventDetectionRuleDefinition[] snapshots = rules?.Select(static rule => rule?.Snapshot() ??
            throw new ArgumentException("Pack rules cannot contain null values.", nameof(rules))).ToArray()
            ?? throw new ArgumentNullException(nameof(rules));
        if (snapshots.Length == 0) {
            throw new ArgumentException("A detection pack must contain at least one rule.", nameof(rules));
        }
        EnsureUniqueRuleIds(snapshots);
        foreach (EventDetectionRuleDefinition rule in snapshots) {
            rule.PackId = normalizedPackId;
            rule.PackVersion = normalizedVersion;
            rule.License = string.IsNullOrWhiteSpace(rule.License) ? normalizedLicense : rule.License;
            rule.SourceKind = string.IsNullOrWhiteSpace(rule.SourceKind) ? "Native" : rule.SourceKind;
            rule.SourceId = string.IsNullOrWhiteSpace(rule.SourceId) ? rule.RuleId : rule.SourceId;
            rule.SourceHash = string.IsNullOrWhiteSpace(rule.SourceHash)
                ? ComputeRuleHash(rule)
                : rule.SourceHash.ToUpperInvariant();
        }
        DateTime normalizedCreated = (createdUtc ?? DateTime.UtcNow).ToUniversalTime();
        string hash = ComputeContentHash(
            normalizedPackId,
            normalizedVersion,
            normalizedMinimum,
            normalizedSchema,
            normalizedAuthors,
            normalizedLicense,
            normalizedCreated,
            snapshots);
        return new EventDetectionPack(
            normalizedPackId,
            normalizedVersion,
            normalizedMinimum,
            normalizedSchema,
            normalizedAuthors,
            normalizedLicense,
            normalizedCreated,
            snapshots,
            hash,
            string.Empty,
            string.Empty);
    }

    /// <summary>Returns a new pack signed by the supplied RSA private key.</summary>
    public EventDetectionPack Sign(RSA privateKey) {
        if (privateKey == null) {
            throw new ArgumentNullException(nameof(privateKey));
        }
        byte[] signature = privateKey.SignHash(
            HexToBytes(ContentHash),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return new EventDetectionPack(
            PackId,
            Version,
            MinimumEngineVersion,
            ObservationSchemaVersion,
            Authors,
            License,
            CreatedUtc,
            _rules.Select(static rule => rule.Snapshot()).ToArray(),
            ContentHash,
            RsaSha256,
            Convert.ToBase64String(signature));
    }

    /// <summary>Validates content integrity and, when possible, the optional signature.</summary>
    public EventDetectionPackValidationResult Validate(RSA? publicKey = null, bool requireSignature = false) {
        var diagnostics = new List<string>();
        string expectedHash = ComputeContentHash(
            PackId,
            Version,
            MinimumEngineVersion,
            ObservationSchemaVersion,
            Authors,
            License,
            CreatedUtc,
            _rules);
        bool hashValid = FixedTimeEquals(ContentHash, expectedHash);
        if (!hashValid) {
            diagnostics.Add("The pack content hash does not match its canonical payload.");
        }
        if (CompareSemanticCore(MinimumEngineVersion, CurrentEngineVersion) > 0) {
            diagnostics.Add(
                $"The pack requires EventViewerX engine {MinimumEngineVersion}, but this engine implements {CurrentEngineVersion}.");
        }
        if (!SemanticContractEquals(ObservationSchemaVersion, CurrentObservationSchemaVersion)) {
            diagnostics.Add(
                $"The pack requires observation schema {ObservationSchemaVersion}, but this engine implements {CurrentObservationSchemaVersion}.");
        }
        EventDetectionPackSignatureStatus signatureStatus;
        if (string.IsNullOrWhiteSpace(Signature)) {
            signatureStatus = EventDetectionPackSignatureStatus.Unsigned;
            if (requireSignature) {
                diagnostics.Add("A verified signature is required, but the pack is unsigned.");
            }
        } else if (!string.Equals(SignatureAlgorithm, RsaSha256, StringComparison.Ordinal)) {
            signatureStatus = EventDetectionPackSignatureStatus.Invalid;
            diagnostics.Add($"Unsupported signature algorithm '{SignatureAlgorithm}'.");
        } else if (publicKey == null) {
            signatureStatus = EventDetectionPackSignatureStatus.Unverified;
            if (requireSignature) {
                diagnostics.Add("A verified signature is required, but no public key was supplied.");
            }
        } else {
            try {
                bool valid = publicKey.VerifyHash(
                    HexToBytes(ContentHash),
                    Convert.FromBase64String(Signature),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                signatureStatus = valid
                    ? EventDetectionPackSignatureStatus.Valid
                    : EventDetectionPackSignatureStatus.Invalid;
                if (!valid) {
                    diagnostics.Add("The pack signature is invalid for the supplied public key.");
                }
            } catch (FormatException) {
                signatureStatus = EventDetectionPackSignatureStatus.Invalid;
                diagnostics.Add("The pack signature is not valid Base64.");
            } catch (CryptographicException) {
                signatureStatus = EventDetectionPackSignatureStatus.Invalid;
                diagnostics.Add("The pack signature could not be verified.");
            }
        }
        return new EventDetectionPackValidationResult(hashValid, signatureStatus, diagnostics);
    }

    /// <summary>Returns immutable rule objects suitable for plan compilation after integrity validation.</summary>
    public IReadOnlyList<IEventDetectionRule> GetRules(
        RSA? publicKey = null,
        bool requireSignature = false) {

        EventDetectionPackValidationResult validation = Validate(publicKey, requireSignature);
        if (!validation.IsValid) {
            throw new InvalidDataException(string.Join(" ", validation.Diagnostics));
        }
        return Array.AsReadOnly(_rules.Select(static rule => (IEventDetectionRule)new EventDetectionRule(rule)).ToArray());
    }

    /// <summary>Compares this pack with a later version without enabling either pack.</summary>
    public EventDetectionPackComparison CompareTo(EventDetectionPack current) {
        if (current == null) {
            throw new ArgumentNullException(nameof(current));
        }
        if (!string.Equals(PackId, current.PackId, StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("Only versions of the same PackId can be compared.", nameof(current));
        }
        Dictionary<string, EventDetectionRuleDefinition> previousRules = _rules.ToDictionary(
            static rule => rule.RuleId,
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, EventDetectionRuleDefinition> currentRules = current._rules.ToDictionary(
            static rule => rule.RuleId,
            StringComparer.OrdinalIgnoreCase);
        string[] added = currentRules.Keys.Except(previousRules.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(static id => id).ToArray();
        string[] removed = previousRules.Keys.Except(currentRules.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(static id => id).ToArray();
        string[] shared = previousRules.Keys.Intersect(currentRules.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        string[] changed = shared.Where(id => !RulesEquivalent(previousRules[id], currentRules[id])).OrderBy(static id => id).ToArray();
        string[] unchanged = shared.Except(changed, StringComparer.OrdinalIgnoreCase).OrderBy(static id => id).ToArray();
        return new EventDetectionPackComparison(PackId, Version, current.Version, added, removed, changed, unchanged);
    }

    /// <summary>Returns the explicit data-source coverage required by this pack.</summary>
    public EventDetectionPackCoverage GetCoverage() {
        EventDetectionStepDefinition[] steps = _rules.SelectMany(static rule => rule.Steps).ToArray();
        EventType[] eventTypes = _rules.SelectMany(static rule => rule.EventTypes)
            .Concat(steps.SelectMany(static step => step.EventTypes))
            .Distinct()
            .OrderBy(static type => type)
            .ToArray();
        IReadOnlyList<EventSourceDefinition> typedSources = EventTypeCatalog.GetSources(eventTypes);
        EventPrerequisite[] prerequisites = eventTypes
            .Select(EventRequirementCatalog.GetRequirement)
            .SelectMany(static requirement => requirement.Prerequisites)
            .GroupBy(static prerequisite => prerequisite.Key, StringComparer.OrdinalIgnoreCase)
            .Select(EventRequirementCatalog.MergePrerequisites)
            .OrderBy(static prerequisite => prerequisite.Kind)
            .ThenBy(static prerequisite => prerequisite.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new EventDetectionPackCoverage(
            eventTypes,
            _rules.SelectMany(static rule => rule.EventIds)
                .Concat(steps.SelectMany(static step => step.EventIds))
                .Concat(typedSources.SelectMany(static source => source.EventIds))
                .Distinct()
                .OrderBy(static id => id)
                .ToArray(),
            _rules.SelectMany(static rule => rule.Channels)
                .Concat(steps.SelectMany(static step => step.Channels))
                .Concat(typedSources.Select(static source => source.LogName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _rules.SelectMany(static rule => rule.Providers)
                .Concat(steps.SelectMany(static step => step.Providers))
                .Concat(typedSources.SelectMany(static source => source.ProviderNames))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            prerequisites);
    }

    /// <summary>Serializes the complete manifest, rules, hash, and optional signature.</summary>
    public string ToJson(bool indented = true) {
        JsonSerializerOptions options = CreateJsonOptions();
        options.WriteIndented = indented;
        return JsonSerializer.Serialize(ToEnvelope(), options);
    }

    /// <summary>Parses a manifest and rules without silently replacing declared integrity values.</summary>
    public static EventDetectionPack ParseJson(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            throw new ArgumentException("Pack JSON cannot be empty.", nameof(json));
        }
        PackEnvelope envelope = JsonSerializer.Deserialize<PackEnvelope>(json, JsonOptions) ??
            throw new InvalidDataException("Pack JSON did not contain an object.");
        string packId = NormalizeRequired(envelope.PackId, nameof(envelope.PackId), 200);
        string version = NormalizeSemanticVersion(envelope.Version, nameof(envelope.Version));
        string minimum = NormalizeSemanticVersion(envelope.MinimumEngineVersion, nameof(envelope.MinimumEngineVersion));
        string schema = NormalizeSemanticVersion(envelope.ObservationSchemaVersion, nameof(envelope.ObservationSchemaVersion));
        string[] authors = NormalizeAuthors(envelope.Authors);
        string license = NormalizeOptional(envelope.License, 200);
        EventDetectionRuleDefinition[] rules = (envelope.Rules ?? Array.Empty<EventDetectionRuleDefinition>())
            .Select(static rule => rule?.Snapshot() ?? throw new InvalidDataException("Pack rules cannot contain null values."))
            .ToArray();
        if (rules.Length == 0) {
            throw new InvalidDataException("A detection pack must contain at least one rule.");
        }
        foreach (EventDetectionRuleDefinition rule in rules) {
            if (!string.Equals(rule.PackId, packId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rule.PackVersion, version, StringComparison.Ordinal)) {
                throw new InvalidDataException(
                    $"Detection rule '{rule.RuleId}' declares pack identity '{rule.PackId}' version " +
                    $"'{rule.PackVersion}', but the manifest declares '{packId}' version '{version}'.");
            }
        }
        EnsureUniqueRuleIds(rules);
        return new EventDetectionPack(
            packId,
            version,
            minimum,
            schema,
            authors,
            license,
            envelope.CreatedUtc.ToUniversalTime(),
            rules,
            NormalizeRequired(envelope.ContentHash, nameof(envelope.ContentHash), 64).ToUpperInvariant(),
            NormalizeOptional(envelope.SignatureAlgorithm, 64),
            NormalizeOptional(envelope.Signature, 16_384));
    }

    /// <summary>Loads a pack manifest from disk.</summary>
    public static EventDetectionPack Load(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Pack path cannot be empty.", nameof(path));
        }
        return ParseJson(File.ReadAllText(Path.GetFullPath(path)));
    }

    private PackEnvelope ToEnvelope() => new() {
        PackId = PackId,
        Version = Version,
        MinimumEngineVersion = MinimumEngineVersion,
        ObservationSchemaVersion = ObservationSchemaVersion,
        Authors = Authors.ToArray(),
        License = License,
        CreatedUtc = CreatedUtc,
        Rules = _rules.Select(static rule => rule.Snapshot()).ToArray(),
        ContentHash = ContentHash,
        SignatureAlgorithm = SignatureAlgorithm,
        Signature = Signature
    };

    private static string ComputeRuleHash(EventDetectionRuleDefinition rule) {
        EventDetectionRuleDefinition snapshot = rule.Snapshot();
        snapshot.PackId = string.Empty;
        snapshot.PackVersion = string.Empty;
        snapshot.SourceHash = string.Empty;
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions));
    }

    private static bool RulesEquivalent(
        EventDetectionRuleDefinition left,
        EventDetectionRuleDefinition right) =>
        string.Equals(left.SourceHash, right.SourceHash, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ComputeRuleHash(left), ComputeRuleHash(right), StringComparison.OrdinalIgnoreCase);

    private static string ComputeContentHash(
        string packId,
        string version,
        string minimumEngineVersion,
        string observationSchemaVersion,
        IReadOnlyList<string> authors,
        string license,
        DateTime createdUtc,
        IReadOnlyList<EventDetectionRuleDefinition> rules) {

        var payload = new PackPayload {
            PackId = packId,
            Version = version,
            MinimumEngineVersion = minimumEngineVersion,
            ObservationSchemaVersion = observationSchemaVersion,
            Authors = authors.ToArray(),
            License = license,
            CreatedUtc = createdUtc.ToUniversalTime(),
            Rules = rules.Select(static rule => rule.Snapshot()).ToArray()
        };
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
    }

    private static string Sha256(byte[] value) {
        using SHA256 algorithm = SHA256.Create();
        return string.Concat(algorithm.ComputeHash(value).Select(static item => item.ToString("X2")));
    }

    private static bool FixedTimeEquals(string left, string right) {
        byte[] leftBytes;
        byte[] rightBytes;
        try {
            leftBytes = HexToBytes(left);
            rightBytes = HexToBytes(right);
        } catch (FormatException) {
            return false;
        }
        if (leftBytes.Length != rightBytes.Length) {
            return false;
        }
        int difference = 0;
        for (int index = 0; index < leftBytes.Length; index++) {
            difference |= leftBytes[index] ^ rightBytes[index];
        }
        return difference == 0;
    }

    private static byte[] HexToBytes(string value) {
        if (string.IsNullOrWhiteSpace(value) || value.Length % 2 != 0) {
            throw new FormatException("A hexadecimal value must contain complete bytes.");
        }
        var bytes = new byte[value.Length / 2];
        for (int index = 0; index < bytes.Length; index++) {
            bytes[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
        }
        return bytes;
    }

    private static string NormalizeRequired(string? value, string name, int maximumLength) {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength) {
            throw new InvalidDataException($"{name} is required and cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }

    private static string NormalizeSemanticVersion(string? value, string name) {
        string normalized = NormalizeRequired(value, name, 64);
        if (!SemanticVersion.IsMatch(normalized)) {
            throw new InvalidDataException($"{name} must be a semantic version.");
        }
        return normalized;
    }

    private static int CompareSemanticCore(string left, string right) {
        BigInteger[] leftParts = ParseSemanticCore(left);
        BigInteger[] rightParts = ParseSemanticCore(right);
        for (int index = 0; index < leftParts.Length; index++) {
            int comparison = leftParts[index].CompareTo(rightParts[index]);
            if (comparison != 0) {
                return comparison;
            }
        }
        return 0;
    }

    private static BigInteger[] ParseSemanticCore(string value) {
        string core = value.Split('-', '+')[0];
        return core.Split('.')
            .Select(static part => BigInteger.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static bool SemanticContractEquals(string left, string right) =>
        string.Equals(
            left.Split('+')[0],
            right.Split('+')[0],
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOptional(string? value, int maximumLength) {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength) {
            throw new InvalidDataException($"A manifest value cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }

    private static string[] NormalizeAuthors(IEnumerable<string>? authors) {
        string[] values = (authors ?? Array.Empty<string>())
            .Where(static author => !string.IsNullOrWhiteSpace(author))
            .Select(static author => author.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Any(static author => author.Length > 300)) {
            throw new InvalidDataException("Pack authors cannot exceed 300 characters.");
        }
        return values;
    }

    private static void EnsureUniqueRuleIds(IEnumerable<EventDetectionRuleDefinition> rules) {
        string[] duplicates = rules.GroupBy(static rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicates.Length != 0) {
            throw new InvalidDataException("Duplicate pack rule IDs: " + string.Join(", ", duplicates) + ".");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions() =>
        EventAnalysisJson.CreateSerializerOptions();

    private class PackPayload {
        public string PackId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string MinimumEngineVersion { get; set; } = string.Empty;
        public string ObservationSchemaVersion { get; set; } = string.Empty;
        public string[] Authors { get; set; } = Array.Empty<string>();
        public string License { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public EventDetectionRuleDefinition[] Rules { get; set; } = Array.Empty<EventDetectionRuleDefinition>();
    }

    private sealed class PackEnvelope : PackPayload {
        public string ContentHash { get; set; } = string.Empty;
        public string SignatureAlgorithm { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
