using System.Text.RegularExpressions;

namespace EventViewerX;

/// <summary>Parses captured DSRegCmd output; never invokes dsregcmd, changes registration, or interprets unknown values as NO.</summary>
public static class EventDsRegAnalyzer {
    private const string Reference = "https://learn.microsoft.com/en-us/entra/identity/devices/troubleshoot-device-dsregcmd";
    /// <summary>Parses up to 1 MiB of captured text and retains section-local fields.</summary>
    public static EventDsRegSnapshot Parse(string text, string evidenceIdentity, string executionContext = "Unknown", DateTimeOffset? capturedAt = null) {
        if (text == null) { throw new ArgumentNullException(nameof(text)); }
        if (Encoding.UTF8.GetByteCount(text) > 1024 * 1024) { throw new ArgumentException("DSRegCmd text exceeds 1 MiB.", nameof(text)); }
        if (executionContext is not ("User" or "SYSTEM" or "Unknown")) { throw new ArgumentException("Declare User, SYSTEM, or Unknown.", nameof(executionContext)); }
        var fields = new List<EventDsRegField>();
        string section = "Unknown";
        using var reader = new StringReader(text);
        int number = 0;
        string? line;
        while ((line = reader.ReadLine()) != null) {
            number++;
            string trimmed = line.Trim();
            if (trimmed.StartsWith("|", StringComparison.Ordinal) && trimmed.EndsWith("|", StringComparison.Ordinal)) { section = trimmed.Trim('|', ' '); continue; }
            int separator = trimmed.IndexOf(':');
            if (separator > 0 && fields.Count < 4096) {
                fields.Add(new EventDsRegField { Section = section, Name = trimmed.Substring(0, separator).Trim(), Value = trimmed.Substring(separator + 1).Trim(), Line = number });
            } else if (fields.Count >= 4096) { throw new InvalidDataException("DSRegCmd field limit exceeded."); }
        }
        return new EventDsRegSnapshot { EvidenceIdentity = evidenceIdentity, ExecutionContext = executionContext, CapturedAt = capturedAt, Fields = fields.ToArray() };
    }

    /// <summary>Produces conclusions gated by explicit intended state and capture context.</summary>
    public static EventDiagnosticFinding[] Analyze(EventDsRegSnapshot snapshot, string? expectedJoin = null) {
        if (snapshot == null) { throw new ArgumentNullException(nameof(snapshot)); }
        if (expectedJoin != null && expectedJoin is not ("Entra" or "Hybrid" or "Domain" or "Unjoined")) { throw new ArgumentException("Unknown intended join state.", nameof(expectedJoin)); }
        var findings = new List<EventDiagnosticFinding>();
        string? Get(string section, string key) {
            string[] values = snapshot.Fields.Where(field => field.Section.Equals(section, StringComparison.OrdinalIgnoreCase) && field.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                .Select(field => field.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return values.Length == 1 ? values[0] : null;
        }
        bool? Flag(string section, string key) => Get(section, key)?.ToUpperInvariant() switch { "YES" => true, "NO" => false, _ => null };
        bool? entra = Flag("Device State", "AzureAdJoined"), domain = Flag("Device State", "DomainJoined");
        string observed = entra.HasValue && domain.HasValue ? entra.Value ? domain.Value ? "Hybrid" : "Entra" : domain.Value ? "Domain" : "Unjoined" : "Unknown";
        bool? enterprise = Flag("Device State", "EnterpriseJoined");
        if (enterprise == true) { observed = entra == true ? "Unknown" : domain == true ? "OnPremisesDrs" : "Unknown"; }
        void Add(string id, string title, string status, string explanation, string artifact, string action, string reason) {
            findings.Add(new EventDiagnosticFinding { RuleId = id, Title = title, Status = status, Explanation = explanation,
                EvidenceIdentities = new[] { snapshot.EvidenceIdentity }, References = new[] { Reference },
                NextChecks = new[] { new EventDiagnosticNextCheck { Artifact = artifact, Action = action, Reason = reason } } });
        }
        Add("endpoint.identity.join", "Captured join state: " + observed,
            expectedJoin == null || observed == "Unknown" ? "InsufficientEvidence" : expectedJoin == observed ? "Observed" : "Failure",
            expectedJoin == null ? "Intended join state was not supplied. Captured registration flags alone do not establish a deployment defect."
                : "Captured state is " + observed + "; intended state is " + expectedJoin + ".",
            "Deployment configuration and a fresh dsregcmd /status capture", "Verify the intended join mode and compare a current capture.", "This is a point-in-time snapshot, not a live registration test.");
        string? auth = Get("Device Details", "DeviceAuthStatus");
        if (auth != null && auth.StartsWith("FAILED", StringComparison.OrdinalIgnoreCase)) {
            Add("endpoint.identity.authentication", "Device authentication check reported a failure",
                auth.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ? "InsufficientEvidence" : "Failure",
                "Captured DeviceAuthStatus: " + auth + ". A check error cannot establish that the directory device was disabled or deleted.",
                "Device registration diagnostics and directory device state", "Inspect the test error and read the device's enabled/present state.", "The capture cannot distinguish all authentication and connectivity causes.");
        }
        if (Flag("SSO State", "AzureAdPrt") == false && entra == true) {
            Add("endpoint.identity.prt", "No PRT in the captured SSO state", "InsufficientEvidence",
                snapshot.ExecutionContext == "User" ? "The user capture reports AzureAdPrt NO. This does not identify the cause or prove the user's current state."
                    : "The capture context is " + snapshot.ExecutionContext + "; user PRT state cannot be judged from this context.",
                "Affected user's dsregcmd /status and sign-in diagnostics", "Capture in the affected user's context and inspect acquisition/refresh diagnostics.", "User SSO needs the correct user context and a capture close to the failure.");
        }
        Add("endpoint.identity.mdm", "MDM enrollment needs separate evidence", "InsufficientEvidence",
            "MDM URLs describe tenant configuration. Their presence or absence alone does not prove device enrollment.",
            "DeviceManagement-Enterprise-Diagnostics-Provider event evidence and enrollment configuration", "Compare enrollment events with the intended MDM scope.", "DSRegCmd tenant URLs are not enrollment receipts.");
        if (Flag("User State", "NgcSet") == false) {
            Add("endpoint.identity.hello", "No Hello key in the captured user state", "InsufficientEvidence",
                "NgcSet NO does not establish a provisioning failure without user context and intended Hello policy.",
                "Hello policy and provisioning diagnostics", "Check the intended policy and capture the affected user's provisioning diagnostics.", "A missing user key may be expected.");
        }
        if (!snapshot.CapturedAt.HasValue) { Add("endpoint.identity.freshness", "Capture time is unknown", "InsufficientEvidence", "No capture instant was supplied.", "Collection receipt", "Record when and under which identity the output was captured.", "Freshness cannot be assessed from a file timestamp."); }
        return findings.ToArray();
    }
}