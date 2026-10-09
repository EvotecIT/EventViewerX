using System.Text.RegularExpressions;

namespace EventViewerX;

/// <summary>Conservative offline IME application reducer. Unattributed messages never inherit an application from a nearby line or thread.</summary>
public static class EventIntuneApplicationAnalyzer {
    private const string Reference = "https://learn.microsoft.com/en-us/troubleshoot/mem/intune/app-management/develop-deliver-working-win32-app-via-intune";
    private const string GuidPattern = "[a-fA-F0-9]{8}-(?:[a-fA-F0-9]{4}-){3}[a-fA-F0-9]{12}";
    private static Regex Pattern(string value) => new(value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex App = Pattern("\\b(?:app(?:lication)?(?:\\s+with)?\\s*(?:id)?\\s*[:=]?\\s+|appid\\s*[:=]\\s*)\\{?(?<id>" + GuidPattern + ")\\}?");
    private static readonly Regex Context = Pattern("\\b(?:user(?:id)?|context)\\s*[:=]\\s*(?<value>SYSTEM|" + GuidPattern + "|[^\\s,;\\]]+)");
    private static readonly Regex Attempt = Pattern("\\b(?:attempt|session)(?:id)?\\s*[:=]\\s*(?<value>[a-zA-Z0-9_-]+)");
    private static readonly Regex Exit = Pattern("\\b(?:lpExitCode|exit\\s*code)\\s*[:=]?\\s*(?<value>0x[a-fA-F0-9]+|-?\\d+)");
    private static readonly Regex EnforcementStarted = Pattern("\\b(?:installation|enforcement)\\s+(?:has\\s+)?(?:started|starts|starting)\\b|\\b(?:starting|executing)\\s+(?:installation|install command|enforcement)\\b");
    private static readonly Regex EnforcementCompleted = Pattern("\\b(?:installation|enforcement)\\s+(?:has\\s+)?(?:completed|finished)\\b");

    /// <summary>Reconstructs bounded attempts and generates next-evidence guidance. Unknown clocks remain source-local; no duration is invented.</summary>
    public static EventEndpointAnalysis Analyze(IEnumerable<EventDiagnosticRecord> records, IEnumerable<EventDsRegSnapshot>? snapshots = null,
        string? expectedJoin = null, bool inputComplete = true, IEnumerable<string>? coverageDiagnostics = null) {
        if (records == null) { throw new ArgumentNullException(nameof(records)); }
        var retained = new List<EventDiagnosticRecord>();
        long size = 0;
        foreach (EventDiagnosticRecord record in records) {
            if (record == null || retained.Count >= 100_000 || (size += Encoding.UTF8.GetByteCount(record.RawText)) > 256L * 1024 * 1024) {
                throw new ArgumentException("Analysis input exceeds finite evidence bounds.", nameof(records));
            }
            retained.Add(record);
        }
        var diagnostics = new List<string>(coverageDiagnostics ?? Array.Empty<string>());
        var attempts = new List<EventIntuneApplicationAttempt>();
        var active = new Dictionary<string, EventIntuneApplicationAttempt>(StringComparer.OrdinalIgnoreCase);
        var evidenceByAttempt = new Dictionary<EventIntuneApplicationAttempt, List<string>>();
        var phasesByAttempt = new Dictionary<EventIntuneApplicationAttempt, List<string>>();
        var completedAttempts = new HashSet<EventIntuneApplicationAttempt>();
        var findings = new List<EventDiagnosticFinding>();
        int unattributed = 0;
        // A source with any unknown instant stays wholly source-local; mixing clock qualities must not reorder its pre-install checks.
        var localSources = new HashSet<(string Source, string Generation)>(retained.Where(record => !record.Timestamp.HasValue).Select(record => (record.Source, record.Generation)));
        IEnumerable<EventDiagnosticRecord> ordered = retained.OrderBy(record => localSources.Contains((record.Source, record.Generation)) ? 1 : 0)
            .ThenBy(record => localSources.Contains((record.Source, record.Generation)) ? (DateTime?)null : record.Timestamp?.UtcDateTime)
            .ThenBy(record => record.Source, StringComparer.Ordinal).ThenBy(record => record.Generation, StringComparer.Ordinal).ThenBy(record => record.ByteStart);
        foreach (EventDiagnosticRecord record in ordered) {
            if (record.Format is "Malformed" or "Oversized") { inputComplete = false; continue; }
            string message = record.Message;
            string phase = Phase(message);
            Match exit = Exit.Match(message);
            if (phase == "Unknown" && !exit.Success) { continue; }
            string[] ids = App.Matches(message).Cast<Match>().Select(match => match.Groups["id"].Value.ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (ids.Length != 1) { unattributed++; continue; }
            Match contextMatch = Context.Match(message), attemptMatch = Attempt.Match(message);
            string context = NormalizeContext(contextMatch.Success ? contextMatch.Groups["value"].Value : string.IsNullOrWhiteSpace(record.Context) ? record.CaptureContext : record.Context);
            string scope = !localSources.Contains((record.Source, record.Generation)) && !string.IsNullOrWhiteSpace(record.Device) && context is not ("Unknown" or "User")
                ? "device:" + record.Device : "source:" + record.Source + ":" + record.Generation;
            string key = ids[0] + "|" + context + "|" + scope + "|" + (attemptMatch.Success ? attemptMatch.Groups["value"].Value : string.Empty);
            bool executionStarts = phase == "Enforcement" && EnforcementStarted.IsMatch(message);
            bool starts = Contains(message, "retry") || executionStarts;
            if (!active.TryGetValue(key, out EventIntuneApplicationAttempt? application) ||
                !attemptMatch.Success && (application.LastPhase == "Reporting" || starts && phasesByAttempt[application].Contains("Enforcement"))) {
                application = new EventIntuneApplicationAttempt {
                    ApplicationId = ids[0], Context = context,
                    AttemptId = attemptMatch.Success ? attemptMatch.Groups["value"].Value : record.Identity,
                    BoundaryQuality = attemptMatch.Success ? "Explicit" : "Inferred"
                };
                active[key] = application; attempts.Add(application);
                evidenceByAttempt[application] = new List<string>(); phasesByAttempt[application] = new List<string>();
            }
            evidenceByAttempt[application].Add(record.Identity);
            if (executionStarts) {
                // A logging session can contain several executions. An earlier result cannot settle a newly started execution.
                completedAttempts.Remove(application); application.Outcome = "Unknown"; application.ExitCode = null; application.ExitDiagnostic = null;
            }
            if (exit.Success || EnforcementCompleted.IsMatch(message)) { completedAttempts.Add(application); }
            if (phase != "Unknown") {
                application.LastPhase = phase;
                if (!phasesByAttempt[application].Contains(phase)) { phasesByAttempt[application].Add(phase); }
            }
            if (exit.Success) {
                application.ExitCode = exit.Groups["value"].Value;
                // MSI semantics require an explicit installer declaration; generic process return-code maps are deployment configuration.
                bool msi = Contains(message, "msiexec") || Contains(message, "Windows Installer") || Contains(message, "MSI exit");
                EventDiagnosticCode code = EventDiagnosticCode.Resolve(application.ExitCode, msi ? EventDiagnosticCodeKind.WindowsInstaller : EventDiagnosticCodeKind.ProcessExit);
                application.ExitDiagnostic = code;
                application.Outcome = code.Outcome switch {
                    "Success" => "InstallerSucceededDetectionUnknown",
                    "SuccessRestartRequired" or "SuccessRestartInitiated" => "RestartRequired",
                    "Failure" => "Failure", _ => "Unknown"
                };
            }
            if (phase == "Applicability" && (Contains(message, "not applicable") || Contains(message, "applicability: false"))) { application.Outcome = "NotApplicable"; }
            if (phase == "Detection" && (Contains(message, "applicationDetected: True") || Contains(message, "applicationDetected=true"))) {
                application.Outcome = application.Outcome == "RestartRequired" ? "RestartRequired" : "Installed";
            }
            if (phase == "Detection" && (Contains(message, "applicationDetected: False") || Contains(message, "applicationDetected=false")) && completedAttempts.Contains(application)) {
                application.Outcome = application.Outcome == "RestartRequired" ? "RestartRequired" : "Failure";
            }
            if (Regex.IsMatch(message, "\\b(?:download|installation|enforcement) (?:has )?failed\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) { application.Outcome = "Failure"; }
        }
        if (unattributed > 0) { diagnostics.Add(unattributed + " phase/result messages lack one explicit application ID; they remain in raw evidence and were not correlated."); }
        if (localSources.Count > 0) { diagnostics.Add("Sources containing an unknown UTC instant remain in byte order for the entire generation. Their cross-file chronology and durations are unknown."); }
        foreach (EventIntuneApplicationAttempt attempt in attempts) {
            attempt.EvidenceIdentities = evidenceByAttempt[attempt].ToArray(); attempt.Phases = phasesByAttempt[attempt].ToArray();
            EventDiagnosticCode? code = attempt.ExitDiagnostic;
            string exitExplanation = code == null ? string.Empty : " Exit code " + code.Original + " (" + code.Kind + ", " + code.Hex +
                (code.SymbolicName == null ? string.Empty : ", " + code.SymbolicName) + "): " + code.Explanation;
            string artifact = attempt.Outcome == "Failure" ? "Installer log, detection configuration, and Intune return-code mapping"
                : attempt.Outcome == "RestartRequired" ? "Reboot policy and post-restart detection evidence" : "Detection result, applicability policy, and reporting receipt";
            findings.Add(new EventDiagnosticFinding {
                RuleId = "endpoint.intune.application", RuleVersion = "1.1.0", Title = attempt.ApplicationId + ": " + attempt.Outcome,
                Status = attempt.Outcome == "Failure" ? "Failure" : attempt.Outcome is "Installed" or "NotApplicable" ? "Observed" : "InsufficientEvidence",
                Explanation = "Last observed phase: " + attempt.LastPhase + ". Outcome: " + attempt.Outcome + ". Attempt boundary: " + attempt.BoundaryQuality + "; context: " + attempt.Context + "." + exitExplanation + " Unobserved phases remain unknown; this does not prove service-side status.",
                EvidenceIdentities = attempt.EvidenceIdentities, References = code == null ? new[] { Reference } : new[] { Reference, code.Reference }.Distinct().ToArray(),
                NextChecks = new[] { new EventDiagnosticNextCheck { Artifact = artifact, Action = "Compare the captured attempt with the configured installation and detection contract.",
                    Reason = "Client observations alone do not establish intended settings, later retries, or accepted service reporting." } }
                    .Concat(code == null ? Array.Empty<EventDiagnosticNextCheck>() : new[] { new EventDiagnosticNextCheck {
                        Artifact = "Installer or process evidence for exit code " + code.Original, Action = code.NextCheck,
                        Reason = "The meaning is bounded by the declared " + code.Kind + " contract; an exit code alone does not establish the deployment's cause or completion." } }).ToArray()
            });
        }
        EventDsRegSnapshot[] identity = (snapshots ?? Array.Empty<EventDsRegSnapshot>()).Take(129).ToArray();
        if (identity.Length > 128) { throw new ArgumentException("Too many identity snapshots.", nameof(snapshots)); }
        foreach (EventDsRegSnapshot snapshot in identity) { findings.AddRange(EventDsRegAnalyzer.Analyze(snapshot, expectedJoin)); }
        if (attempts.Count == 0 && retained.Count > 0) {
            findings.Add(new EventDiagnosticFinding { RuleId = "endpoint.intune.coverage", Title = "No attributable application lifecycle", Explanation = "The supplied records did not provide an explicitly attributable recognized application phase. This does not prove that no deployment ran.",
                NextChecks = new[] { new EventDiagnosticNextCheck { Artifact = "IME application logs for the affected time and app", Action = "Collect AppWorkload, AppActionProcessor, and IntuneManagementExtension logs with application IDs.", Reason = "Recognized, attributable lifecycle evidence is missing." } } });
        }
        return new EventEndpointAnalysis { Records = retained.ToArray(), IdentitySnapshots = identity, Applications = attempts.ToArray(), Findings = findings.ToArray(), InputComplete = inputComplete, CoverageDiagnostics = diagnostics.Distinct().ToArray() };
    }

    private static bool Contains(string text, string value) => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    private static string NormalizeContext(string context) {
        if (string.IsNullOrWhiteSpace(context)) { return "Unknown"; }
        context = context.Trim();
        foreach (string reserved in new[] { "Unknown", "User", "SYSTEM" }) {
            if (context.Equals(reserved, StringComparison.OrdinalIgnoreCase)) { return reserved; }
        }
        return context;
    }
    private static string Phase(string text) {
        if (Contains(text, "reporting") || Contains(text, "send results")) { return "Reporting"; }
        if (Contains(text, "applicationDetected") || Contains(text, "detection")) { return "Detection"; }
        if (Contains(text, "applicability") || Contains(text, "not applicable")) { return "Applicability"; }
        if (Contains(text, "requirement")) { return "Requirements"; }
        if (Contains(text, "dependency")) { return "Dependencies"; }
        if (Contains(text, "download")) { return "Download"; }
        if (Contains(text, "install") || Contains(text, "enforcement") || Contains(text, "lpExitCode") || Contains(text, "exit code")) { return "Enforcement"; }
        if (Contains(text, "reboot") || Contains(text, "restart")) { return "Restart"; }
        return "Unknown";
    }
}