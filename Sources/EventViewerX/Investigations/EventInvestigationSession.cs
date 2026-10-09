using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EventViewerX;

/// <summary>Creates and reopens portable evidence sessions. Replay reads verified canonical observations and never modifies original artifacts.</summary>
public sealed partial class EventInvestigationSession {
    private static readonly JsonSerializerOptions JsonOptions = EventAnalysisJson.CreateSerializerOptions(true);
    private static readonly JsonSerializerOptions CompactOptions = EventAnalysisJson.CreateSerializerOptions();
    private readonly EventInvestigationManifest _manifest;
    private EventInvestigationSession(string directory, EventInvestigationManifest manifest) { DirectoryPath = directory; _manifest = manifest; }
    /// <summary>Absolute session directory.</summary>
    public string DirectoryPath { get; }
    /// <summary>Detached manifest suitable for inspection or export.</summary>
    public EventInvestigationManifest Manifest => Clone(_manifest);
    /// <summary>Exact engine build identity; semantic version alone cannot identify a locally modified parser or evaluator.</summary>
    public static string CurrentEngineIdentity => typeof(EventObject).Assembly.GetName().Version + ":" + typeof(EventObject).Module.ModuleVersionId.ToString("D");

    /// <summary>Creates a new session, copies original input files, and writes observations, effective plan, and original findings. Existing directories are rejected.</summary>
    public static EventInvestigationSession Create(string directory, EventInvestigationManifest request,
        IEnumerable<EventObservation> observations, EventDetectionPlan plan, IEnumerable<string>? inputFiles = null,
        EventDetectionCoverage? coverage = null, CancellationToken cancellationToken = default) {
        return CreateCore(directory, request, observations, plan, inputFiles, coverage, cancellationToken);
    }

    private static EventInvestigationSession CreateCore(string directory, EventInvestigationManifest request,
        IEnumerable<EventObservation> observations, EventDetectionPlan plan, IEnumerable<string>? inputFiles,
        EventDetectionCoverage? coverage, CancellationToken cancellationToken, EventEndpointCapture? endpoint = null) {
        if (request == null) { throw new ArgumentNullException(nameof(request)); }
        if (observations == null) { throw new ArgumentNullException(nameof(observations)); }
        if (plan == null) { throw new ArgumentNullException(nameof(plan)); }
        EventInvestigationManifest manifest = Clone(request);
        manifest.SchemaVersion = 1; manifest.SessionId = Guid.NewGuid(); manifest.CreatedUtc = DateTime.UtcNow;
        manifest.PlanHash = plan.PlanHash; manifest.EngineIdentity = CurrentEngineIdentity;
        manifest.Artifacts = Array.Empty<EventInvestigationArtifact>(); manifest.ObservationCount = 0;
        manifest.EndpointEvidenceVersion = endpoint == null ? null : 1;
        EventDetectionCoverage effectiveCoverage = coverage ?? EventDetectionCoverage.Unknown();
        if (manifest.Sources?.Any(source => source == null || !source.IsComplete) == true) {
            effectiveCoverage = effectiveCoverage.WithFailures(new[] { "One or more investigation sources are incomplete." });
        }
        manifest.CoverageJson = effectiveCoverage.ToJson();
        Validate(manifest);
        string root = System.IO.Path.GetFullPath(directory);
        if (Directory.Exists(root) || File.Exists(root)) { throw new IOException("An investigation must be created in a new directory."); }
        Directory.CreateDirectory(root);
        var artifacts = new List<EventInvestigationArtifact>();
        var session = new EventInvestigationSession(root, manifest);
        int inputIndex = 0;
        long inputBytes = 0;
        foreach (string input in inputFiles ?? Array.Empty<string>()) {
            cancellationToken.ThrowIfCancellationRequested();
            string name = "input-" + (++inputIndex).ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".evidence";
            using (var source = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var target = new FileStream(session.Resolve(name), FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                var buffer = new byte[81920];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) != 0) {
                    cancellationToken.ThrowIfCancellationRequested();
                    inputBytes += read;
                    if (endpoint != null && inputBytes > endpoint.MaximumInputBytes) { throw new InvalidDataException("Endpoint input byte limit exceeded."); }
                    target.Write(buffer, 0, read);
                }
                target.Flush(true);
            }
            EventInvestigationArtifact artifact = session.Describe(name, "input", cancellationToken);
            artifact.OriginalName = System.IO.Path.GetFileName(input);
            artifacts.Add(artifact);
        }
        string planJson = plan.ToJson();
        if (Encoding.UTF8.GetByteCount(planJson) > 16 * 1024 * 1024) { throw new InvalidDataException("Investigation plan exceeds 16 MiB."); }
        session.WriteNew("plan.json", planJson);
        artifacts.Add(session.Describe("plan.json", "plan", cancellationToken));
        var retained = new List<EventObservation>();
        using (var stream = new FileStream(session.Resolve("observations.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) {
            foreach (EventObservation observation in observations) {
                cancellationToken.ThrowIfCancellationRequested();
                if (observation == null) { throw new ArgumentException("Observations cannot contain null.", nameof(observations)); }
                if (retained.Count >= manifest.Limits.MaximumObservations) { throw new InvalidDataException("Investigation observation limit exceeded."); }
                if (observation.EventTimeUtc < manifest.StartUtc || observation.EventTimeUtc >= manifest.EndUtc) {
                    throw new InvalidDataException("Observation is outside the declared investigation window. Include correlation warm-up in the declared window.");
                }
                EventObservationSnapshot snapshot = EventObservationSnapshot.Capture(observation);
                string json = JsonSerializer.Serialize(snapshot, CompactOptions);
                if (Encoding.UTF8.GetByteCount(json) > manifest.Limits.MaximumObservationBytes) { throw new InvalidDataException("Investigation observation byte limit exceeded."); }
                writer.WriteLine(json);
                // Evaluate the exact durable form, preventing source mutation after capture from changing the original output.
                retained.Add(snapshot.Restore());
            }
            writer.Flush(); stream.Flush(true);
        }
        manifest.ObservationCount = retained.Count;
        artifacts.Add(session.Describe("observations.jsonl", "observations", cancellationToken));
        EventDetectionExecutionResult result = session.Evaluate(retained, plan, effectiveCoverage, cancellationToken);
        using (var stream = new FileStream(session.Resolve("findings.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) {
            foreach (EventDetectionFinding finding in result.Findings) {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteLine(EventAnalysisJson.Serialize(finding));
            }
            writer.Flush(); stream.Flush(true);
        }
        artifacts.Add(session.Describe("findings.jsonl", "output", cancellationToken));
        if (endpoint != null) { session.WriteEndpoint(endpoint, artifacts, cancellationToken); }
        manifest.Artifacts = artifacts.ToArray();
        // The manifest is the commit marker; failed creation never leaves a seemingly complete session.
        string manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        if (Encoding.UTF8.GetByteCount(manifestJson) > 4 * 1024 * 1024) { throw new InvalidDataException("Investigation manifest exceeds 4 MiB."); }
        session.WriteNew("manifest.json", manifestJson);
        return session;
    }

    /// <summary>Opens a session and verifies all input and output hashes before returning it.</summary>
    public static EventInvestigationSession Open(string directory, CancellationToken cancellationToken = default) {
        string root = System.IO.Path.GetFullPath(directory);
        var session = new EventInvestigationSession(root, new EventInvestigationManifest());
        EventInvestigationManifest manifest = JsonSerializer.Deserialize<EventInvestigationManifest>(session.ReadText("manifest.json", 4 * 1024 * 1024, false), JsonOptions)
            ?? throw new InvalidDataException("Missing investigation manifest.");
        Validate(manifest);
        if (manifest.Artifacts.Length < 3) { throw new InvalidDataException("Investigation artifact inventory is missing."); }
        session = new EventInvestigationSession(root, manifest);
        session.Verify(cancellationToken);
        return session;
    }

    /// <summary>Re-evaluates verified canonical observations using the retained plan and limits. A different engine build requires explicit opt-in.</summary>
    public EventDetectionExecutionResult Replay(bool allowDifferentEngine = false, CancellationToken cancellationToken = default) {
        if (!allowDifferentEngine && _manifest.EngineIdentity != CurrentEngineIdentity) {
            throw new InvalidDataException("Engine build differs from the investigation. Explicitly opt in to comparative replay or use the original build.");
        }
        Verify(cancellationToken);
        EventDetectionPlan plan = EventDetectionPlan.FromJson(ReadText("plan.json", 16 * 1024 * 1024, true));
        if (plan.PlanHash != _manifest.PlanHash) { throw new InvalidDataException("Manifest plan identity mismatch."); }
        EventDetectionCoverage coverage = EventDetectionCoverage.FromJson(_manifest.CoverageJson);
        if (_manifest.EngineIdentity != CurrentEngineIdentity) { coverage = coverage.WithFailures(new[] { "Comparative replay used a different engine build." }); }
        return Evaluate(ReadObservations(cancellationToken).ToList(), plan, coverage, cancellationToken);
    }

    private EventDetectionExecutionResult Evaluate(List<EventObservation> observations, EventDetectionPlan plan,
        EventDetectionCoverage coverage, CancellationToken token) {
        observations.Sort((left, right) => {
            int comparison = left.EventTimeUtc.CompareTo(right.EventTimeUtc);
            if (comparison == 0) { comparison = Nullable.Compare(left.RecordId, right.RecordId); }
            return comparison != 0 ? comparison : string.Compare(left.Identity, right.Identity, StringComparison.Ordinal);
        });
        EventDetectionEngineOptions options = _manifest.Limits.Options(coverage)
            .WithAbsenceWindow(new EventDetectionAbsenceWindow(_manifest.EndUtc, _manifest.Sources));
        EventDetectionFinding[] findings = EventDetectionEngine.Stream(observations, plan, options, token)
            .Take(_manifest.Limits.MaximumFindings + 1).ToArray();
        if (findings.Length > _manifest.Limits.MaximumFindings) { throw new InvalidDataException("Investigation finding limit exceeded."); }
        token.ThrowIfCancellationRequested();
        return new EventDetectionExecutionResult(observations, findings, coverage);
    }

    /// <summary>Verifies the byte identity of every original and derived artifact without rewriting it.</summary>
    public void Verify(CancellationToken cancellationToken = default) {
        foreach (EventInvestigationArtifact artifact in _manifest.Artifacts) {
            cancellationToken.ThrowIfCancellationRequested();
            EventInvestigationArtifact actual = Describe(artifact.Path, artifact.Role, cancellationToken);
            if (actual.Length != artifact.Length || !string.Equals(actual.Sha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException($"Investigation artifact '{artifact.Path}' failed integrity verification.");
            }
        }
    }

    private IEnumerable<EventObservation> ReadObservations(CancellationToken token) {
        using var file = new FileStream(Resolve("observations.jsonl"), FileMode.Open, FileAccess.Read, FileShare.Read);
        using SHA256 hash = SHA256.Create();
        using var verified = new CryptoStream(file, hash, CryptoStreamMode.Read);
        using var reader = new StreamReader(verified, new UTF8Encoding(false, true));
        int count = 0;
        // Bound each line before materialization, including deliberately malformed files.
        var line = new StringBuilder();
        int character;
        while ((character = reader.Read()) != -1) {
            token.ThrowIfCancellationRequested();
            if (character != '\n') {
                line.Append((char)character);
                if (line.Length > _manifest.Limits.MaximumObservationBytes) { throw new InvalidDataException("Observation line exceeds its limit."); }
                continue;
            }
            if (++count > _manifest.Limits.MaximumObservations) { throw new InvalidDataException("Observation count exceeds its limit."); }
            string json = line.ToString(); line.Clear();
            if (Encoding.UTF8.GetByteCount(json.TrimEnd('\r')) > _manifest.Limits.MaximumObservationBytes) { throw new InvalidDataException("Observation bytes exceed their limit."); }
            EventObservation observation = (JsonSerializer.Deserialize<EventObservationSnapshot>(json, CompactOptions)
                ?? throw new InvalidDataException("Missing observation.")).Restore();
            if (observation.EventTimeUtc < _manifest.StartUtc || observation.EventTimeUtc >= _manifest.EndUtc) { throw new InvalidDataException("Observation outside investigation window."); }
            yield return observation;
        }
        if (line.Length != 0 || count != _manifest.ObservationCount) { throw new InvalidDataException("Investigation observation stream is incomplete."); }
        VerifyHash("observations.jsonl", file.Position, hash.Hash!);
    }

    private string ReadText(string name, int maximumBytes, bool verify, bool capturedText = false) {
        using var file = new FileStream(Resolve(name), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > maximumBytes) { throw new InvalidDataException("Investigation document exceeds its size limit."); }
        using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        int count;
        while ((count = file.Read(bytes, 0, bytes.Length)) > 0) {
            if (buffer.Length + count > maximumBytes) { throw new InvalidDataException("Investigation document exceeds its size limit."); }
            buffer.Write(bytes, 0, count);
        }
        byte[] content = buffer.ToArray();
        if (verify) { using SHA256 hash = SHA256.Create(); VerifyHash(name, content.Length, hash.ComputeHash(content)); }
        return capturedText ? DecodeCapturedText(content) : new UTF8Encoding(false, true).GetString(content);
    }

    private void VerifyHash(string name, long length, byte[] hash) {
        EventInvestigationArtifact expected = _manifest.Artifacts.Single(item => item.Path == name);
        if (length != expected.Length || !string.Equals(expected.Sha256, BitConverter.ToString(hash).Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException($"Investigation artifact '{name}' failed integrity verification.");
        }
    }

    private EventInvestigationArtifact Describe(string path, string role, CancellationToken token) {
        using var stream = new FileStream(Resolve(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        return new EventInvestigationArtifact { Path = path, Role = role, Length = stream.Length,
            Sha256 = BitConverter.ToString(HashArtifact(stream, token)).Replace("-", string.Empty) };
    }

    internal static byte[] HashArtifact(Stream stream, CancellationToken token) {
        using SHA256 hash = SHA256.Create();
        var buffer = new byte[81920];
        while (true) {
            token.ThrowIfCancellationRequested();
            int count = stream.Read(buffer, 0, buffer.Length);
            token.ThrowIfCancellationRequested();
            if (count == 0) { break; }
            hash.TransformBlock(buffer, 0, count, buffer, 0);
        }
        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return hash.Hash!;
    }
    private void WriteNew(string path, string text) {
        using var stream = new FileStream(Resolve(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] bytes = new UTF8Encoding(false).GetBytes(text); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
    }
    private string Resolve(string relative) {
        // Session artifacts deliberately use flat names, preventing traversal and directory-link escapes.
        if (string.IsNullOrWhiteSpace(relative) || relative.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || relative is "." or "..") {
            throw new InvalidDataException("Investigation artifacts must have a plain relative filename.");
        }
        for (DirectoryInfo? parent = new DirectoryInfo(DirectoryPath); parent != null; parent = parent.Parent) {
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) { throw new InvalidDataException("Linked investigation paths are not supported."); }
        }
        string path = System.IO.Path.Combine(DirectoryPath, relative);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) { throw new InvalidDataException("Linked artifacts are not supported."); }
        return path;
    }
    private static EventInvestigationManifest Clone(EventInvestigationManifest manifest) =>
        JsonSerializer.Deserialize<EventInvestigationManifest>(JsonSerializer.Serialize(manifest, JsonOptions), JsonOptions)!;
    private static void Validate(EventInvestigationManifest manifest) {
        if (manifest.SchemaVersion != 1 || manifest.StartUtc.Kind != DateTimeKind.Utc || manifest.EndUtc.Kind != DateTimeKind.Utc ||
            manifest.EndUtc <= manifest.StartUtc || string.IsNullOrWhiteSpace(manifest.QueryIdentity) ||
            manifest.Sources == null || manifest.Artifacts == null || manifest.Limits == null || manifest.ParserVersions == null) {
            throw new InvalidDataException("Invalid investigation manifest contract.");
        }
        if (manifest.EndpointEvidenceVersion.HasValue && manifest.EndpointEvidenceVersion != 1) { throw new InvalidDataException("Unsupported endpoint evidence version."); }
        foreach (EventCoverageWindow source in manifest.Sources) {
            if (source == null) { throw new InvalidDataException("Investigation sources cannot contain null receipts."); }
            source.Validate();
        }
        _ = manifest.Limits.Options(EventDetectionCoverage.FromJson(manifest.CoverageJson));
        if (manifest.Artifacts.Any(item => item == null || item.Length < 0 || string.IsNullOrWhiteSpace(item.Path) || item.Sha256 == null || item.Sha256.Length != 64) ||
            manifest.ObservationCount < 0 || manifest.ObservationCount > manifest.Limits.MaximumObservations ||
            manifest.Artifacts.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Artifacts.Length) {
            throw new InvalidDataException("Invalid investigation artifact inventory.");
        }
        if (manifest.Artifacts.Length > 0 && new[] { "plan.json", "observations.jsonl", "findings.jsonl" }
            .Any(required => !manifest.Artifacts.Any(item => item.Path == required))) { throw new InvalidDataException("Required investigation artifacts are missing."); }
        if (manifest.EndpointEvidenceVersion.HasValue && manifest.Artifacts.Length > 0 &&
            new[] { "endpoint-records.jsonl", "endpoint-capture.json", "endpoint-analysis.json" }.Any(required => !manifest.Artifacts.Any(item => item.Path == required))) {
            throw new InvalidDataException("Required endpoint evidence artifacts are missing.");
        }
    }
}
