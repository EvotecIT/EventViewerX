namespace EventViewerX;

/// <summary>Creates a decision-oriented report without rerunning an event query or requiring storage.</summary>
public static class EventDecisionReportEngine {
    /// <summary>Filters existing observations, findings, and packs through one built-in report profile.</summary>
    public static EventDecisionReportSnapshot Create(
        EventDecisionReportKind kind,
        IEnumerable<EventObservation>? observations,
        IEnumerable<EventDetectionFinding>? findings,
        IEnumerable<EventDetectionPack>? packs = null,
        EventDetectionReportOptions? options = null) {

        EventDecisionReportDefinition definition = EventDecisionReportCatalog.GetDefinition(kind);
        EventObservation[] allObservations = (observations ?? Array.Empty<EventObservation>()).ToArray();
        EventDetectionFinding[] allFindings = (findings ?? Array.Empty<EventDetectionFinding>()).ToArray();
        EventDetectionPack[] allPacks = (packs ?? Array.Empty<EventDetectionPack>()).ToArray();
        if (allObservations.Any(static item => item == null) ||
            allFindings.Any(static item => item == null) ||
            allPacks.Any(static item => item == null)) {
            throw new ArgumentException("Report inputs cannot contain null values.");
        }

        EventObservation[] selectedObservations = allObservations
            .Where(observation => SelectObservation(definition, observation))
            .ToArray();
        EventDetectionFinding[] selectedFindings = allFindings
            .Where(finding => SelectFinding(definition, finding))
            .ToArray();
        EventObservation[] reportObservations = selectedObservations
            .Concat(selectedFindings.SelectMany(static finding => finding.Evidence))
            .GroupBy(static observation => observation.Identity, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static observation => observation.EventTimeUtc)
            .ToArray();
        EventDetectionPack[] selectedPacks = allPacks
            .Where(pack => SelectPack(definition, pack))
            .ToArray();
        options ??= new EventDetectionReportOptions();
        var effectiveOptions = new EventDetectionReportOptions(
            string.IsNullOrWhiteSpace(options.Title) ||
            string.Equals(options.Title, "EventViewerX detection report", StringComparison.Ordinal)
                ? definition.Title
                : options.Title,
            options.QueryOwner,
            options.UsedStorageHistory,
            options.Limits,
            options.Failures,
            options.Coverage);
        EventDecisionMetric[] metrics = BuildMetrics(
            definition,
            reportObservations,
            selectedFindings,
            selectedPacks);
        EventDetectionReportSnapshot report = EventDetectionReportEngine.Create(
            reportObservations,
            selectedFindings,
            selectedPacks,
            effectiveOptions,
            metrics);
        return new EventDecisionReportSnapshot(definition, metrics, report);
    }

    private static bool SelectObservation(
        EventDecisionReportDefinition definition,
        EventObservation observation) {

        if (definition.IncludeAllObservations) {
            return true;
        }
        if (definition.Kind == EventDecisionReportKind.UnknownEventAndSchemaDrift) {
            return string.Equals(observation.TypeName, "Generic", StringComparison.OrdinalIgnoreCase);
        }
        return Enum.TryParse(observation.TypeName, ignoreCase: true, out EventType type) &&
               definition.EventTypes.Contains(type);
    }

    private static bool SelectFinding(
        EventDecisionReportDefinition definition,
        EventDetectionFinding finding) {

        if (definition.IncludeAllFindings) {
            return true;
        }
        if (definition.Kind == EventDecisionReportKind.UnknownEventAndSchemaDrift) {
            return finding.Status != EventDetectionFindingStatus.Matched ||
                   !string.IsNullOrWhiteSpace(finding.CompletenessDiagnostic);
        }
        return definition.PackIds.Contains(finding.PackId, StringComparer.OrdinalIgnoreCase) ||
               finding.Tags.Any(tag => definition.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) ||
               finding.Evidence.Any(observation => SelectObservation(definition, observation));
    }

    private static bool SelectPack(
        EventDecisionReportDefinition definition,
        EventDetectionPack pack) {

        if (definition.IncludeAllPacks ||
            definition.PackIds.Contains(pack.PackId, StringComparer.OrdinalIgnoreCase)) {
            return true;
        }
        return pack.Rules.Any(rule =>
            rule.EventTypes.Any(definition.EventTypes.Contains) ||
            rule.Tags.Any(tag => definition.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    private static EventDecisionMetric[] BuildMetrics(
        EventDecisionReportDefinition definition,
        IReadOnlyList<EventObservation> observations,
        IReadOnlyList<EventDetectionFinding> findings,
        IReadOnlyList<EventDetectionPack> packs) {

        var metrics = new List<EventDecisionMetric> {
            Metric("ObservationCount", "Observations", observations.Count, "count", "Canonical observations represented by this report."),
            Metric("FindingCount", "Findings", findings.Count, "count", "Matched, incomplete, and error findings represented by this report."),
            Metric("MatchedFindingCount", "Matched findings", findings.Count(static finding => finding.Status == EventDetectionFindingStatus.Matched), "count", "Rules that produced a complete match."),
            Metric("IncompleteFindingCount", "Incomplete findings", findings.Count(static finding => finding.Status == EventDetectionFindingStatus.Incomplete), "count", "Rules that could not reach a complete conclusion."),
            Metric("ErrorFindingCount", "Error findings", findings.Count(static finding => finding.Status == EventDetectionFindingStatus.Error), "count", "Rule or engine errors requiring investigation."),
            Metric("TargetCount", "Targets", observations.SelectMany(static item => new[] { item.SourceComputer, item.CollectorComputer }).Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "count", "Distinct source and collector computers represented."),
            Metric("ChannelCount", "Channels", observations.Select(static item => item.SourceLog).Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "count", "Distinct original event channels represented."),
            Metric("PackCount", "Detection packs", packs.Count, "count", "Enabled versioned packs represented by this profile.")
        };
        if (observations.Count > 0) {
            double[] receiveLag = observations
                .Select(static observation => Math.Max(0, (observation.ReceivedTimeUtc - observation.EventTimeUtc).TotalSeconds))
                .ToArray();
            metrics.Add(Metric("AverageReceiveLagSeconds", "Average receive lag", receiveLag.Average(), "seconds", "Average time from source event creation to receipt."));
            metrics.Add(Metric("MaximumReceiveLagSeconds", "Maximum receive lag", receiveLag.Max(), "seconds", "Largest represented source-to-receipt delay."));
        }

        foreach ((string name, string displayName, double value, string description) in
                 BuildProfileCounters(definition.Kind, observations, findings, packs)) {
            metrics.Add(Metric(name, displayName, value, "count", description));
        }
        return metrics.ToArray();
    }

    private static IEnumerable<(string Name, string DisplayName, double Value, string Description)> BuildProfileCounters(
        EventDecisionReportKind kind,
        IReadOnlyList<EventObservation> observations,
        IReadOnlyList<EventDetectionFinding> findings,
        IReadOnlyList<EventDetectionPack> packs) {

        long CountType(params EventType[] types) {
            var names = new HashSet<string>(types.Select(static type => type.ToString()), StringComparer.OrdinalIgnoreCase);
            return observations.LongCount(observation => names.Contains(observation.TypeName));
        }
        long CountTag(params string[] tags) {
            var names = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            return findings.LongCount(finding => finding.Tags.Any(names.Contains));
        }

        if (kind == EventDecisionReportKind.CollectionCoverage) {
            yield return ("ProviderCount", "Providers", observations.Select(static item => item.ProviderName).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Distinct event providers represented.");
            yield return ("GenericObservationCount", "Unmapped observations", observations.LongCount(static item => item.TypeName == "Generic"), "Events without a typed projection.");
        } else if (kind == EventDecisionReportKind.EventingIntegrity) {
            yield return ("ClearedLogCount", "Cleared logs", CountType(EventType.LogsClearedSecurity, EventType.LogsClearedOther), "Security, Application, or System log clear events.");
            yield return ("AuditPolicyChangeCount", "Audit policy changes", CountType(EventType.AuditPolicyChange), "Observed audit policy changes.");
            yield return ("FullSecurityLogCount", "Full Security logs", CountType(EventType.LogsFullSecurity), "Security log capacity events.");
        } else if (kind == EventDecisionReportKind.AuthenticationPosture) {
            yield return ("NtlmV1Count", "NTLMv1 events", CountType(EventType.ADUserLogonNTLMv1), "Successful logons explicitly identified as NTLMv1.");
            yield return ("FailedLogonCount", "Failed logons", CountType(EventType.ADUserLogonFailed), "Failed authentication events.");
            yield return ("SuccessfulLogonCount", "Successful logons", CountType(
                EventType.ADUserLogon,
                EventType.ADUserLogonNTLMv1), "Successful authentication events.");
            yield return ("WeakKerberosFindingCount", "Weak Kerberos findings", CountTag("kerberos-weak-encryption"), "Findings involving weak Kerberos encryption.");
            yield return ("LdapSigningFindingCount", "LDAP signing findings", CountTag("ldap-signing"), "Unsigned or cleartext LDAP bind findings.");
        } else if (kind == EventDecisionReportKind.IdentityLifecycle) {
            yield return ("AccountStateChangeCount", "Account state changes", CountType(EventType.ADUserStatus), "Enable, disable, lock, unlock, and related state events.");
            yield return ("MembershipChangeCount", "Membership changes", CountType(EventType.ADGroupMembershipChange), "Group membership changes represented in the window.");
            yield return ("DistinctAccountCount", "Distinct accounts", CountDistinctFields(observations, "ObjectAffected", "Who"), "Distinct actor and target account values.");
        } else if (kind == EventDecisionReportKind.PrivilegedAccess) {
            yield return ("PrivilegedMembershipFindingCount", "Privileged membership findings", CountTag("privilege"), "Findings tagged for privilege changes.");
            yield return ("RightsAssignmentCount", "Rights assignments", CountType(EventType.ADUserRightsAssignment), "User-right assignment changes.");
            yield return ("SpecialPrivilegeLogonCount", "Special privilege logons", CountType(EventType.ADUserPrivilegeUse), "Logons receiving sensitive privileges.");
        } else if (kind == EventDecisionReportKind.GroupPolicyGovernance) {
            yield return ("GpoCreatedCount", "GPOs created", CountType(EventType.GpoCreated), "Group Policy Object creation events.");
            yield return ("GpoModifiedCount", "GPOs modified", CountType(EventType.GpoModified, EventType.ADGroupPolicyEdits, EventType.ADGroupPolicyChangesDetailed), "Group Policy edits and detailed changes.");
            yield return ("GpoLinkChangeCount", "GPO link changes", CountType(EventType.ADGroupPolicyLinks), "Link, disable, or enforcement flag changes.");
            yield return ("GpoDeletedCount", "GPOs deleted", CountType(EventType.GpoDeleted), "Group Policy Object deletion events.");
        } else if (kind == EventDecisionReportKind.CertificateServicesGovernance) {
            yield return ("CertificateIssuedCount", "Certificates issued", CountType(EventType.CertificateIssued), "Certificate issuance audit events.");
        } else if (kind == EventDecisionReportKind.ExecutionAndPersistence) {
            yield return ("ScheduledTaskCount", "Scheduled task events", CountType(EventType.ScheduledTaskCreated, EventType.ScheduledTaskUpdated, EventType.ScheduledTaskEnabled, EventType.ScheduledTaskDisabled, EventType.ScheduledTaskDeleted), "Scheduled task lifecycle activity.");
            yield return ("FirewallRuleCount", "Firewall rule events", CountType(EventType.FirewallRuleAdded, EventType.FirewallRuleDeleted, EventType.FirewallRuleChange), "Firewall rule lifecycle activity.");
            yield return ("DefenderCount", "Defender events", CountType(EventType.DefenderThreatDetected, EventType.DefenderThreatAction, EventType.DefenderConfigurationChanged), "Threat, action, and configuration events.");
        } else if (kind == EventDecisionReportKind.DetectionHealth) {
            yield return ("ValidPackHashCount", "Valid pack hashes", packs.LongCount(static pack => pack.Validate().ContentHashValid), "Packs whose content matches the declared hash.");
            yield return ("FindingWithCompletenessDiagnosticCount", "Completeness diagnostics", findings.LongCount(static finding => !string.IsNullOrWhiteSpace(finding.CompletenessDiagnostic)), "Findings carrying an explicit incomplete-result reason.");
        } else if (kind == EventDecisionReportKind.UnknownEventAndSchemaDrift) {
            EventObservation[] generic = observations
                .Where(static item => string.Equals(item.TypeName, "Generic", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            yield return ("UnknownProviderCount", "Unknown providers", generic.Select(static item => item.ProviderName).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Providers contributing unmapped observations.");
            yield return ("UnknownEventShapeCount", "Unknown event shapes", generic.Select(static item => item.ProviderName + "\0" + item.EventId).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Distinct provider and event-ID pairs without a selected typed contract.");
        } else if (kind == EventDecisionReportKind.IncidentTimeline) {
            yield return ("EvidenceIdentityCount", "Evidence identities", findings.SelectMany(static finding => finding.EvidenceIdentities).Distinct(StringComparer.Ordinal).Count(), "Distinct raw evidence identities retained by findings.");
            yield return ("PivotCount", "Pivot values", findings.SelectMany(static finding => finding.Entities).Select(static item => item.Key + "\0" + item.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Distinct actor, target, host, account, and related pivot values.");
        }
    }

    private static long CountDistinctFields(
        IEnumerable<EventObservation> observations,
        params string[] fields) => observations
            .SelectMany(observation => fields.Select(field =>
                observation.Fields.TryGetValue(field, out object? value)
                    ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                    : null))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .LongCount();

    private static EventDecisionMetric Metric(
        string name,
        string displayName,
        double value,
        string unit,
        string description) => new(name, displayName, value, unit, description);
}

/// <summary>One report profile together with its complete renderer-ready analysis snapshot.</summary>
public sealed class EventDecisionReportSnapshot {
    internal EventDecisionReportSnapshot(
        EventDecisionReportDefinition definition,
        IReadOnlyList<EventDecisionMetric> metrics,
        EventDetectionReportSnapshot analysis) {

        Definition = definition;
        Metrics = Array.AsReadOnly(metrics.ToArray());
        Analysis = analysis;
    }

    /// <summary>Selected report profile and declared data scope.</summary>
    public EventDecisionReportDefinition Definition { get; }
    /// <summary>Profile-specific and common numeric decision metrics.</summary>
    public IReadOnlyList<EventDecisionMetric> Metrics { get; }
    /// <summary>Filtered observations, findings, pack coverage, timeline, and presentation report.</summary>
    public EventDetectionReportSnapshot Analysis { get; }
}
