using EventViewerX.Native;
using EventViewerX.Sigma;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestSigmaDetection {
    [Fact]
    public void SigmaSelectionsConditionsAndModifiersCompileToNativePredicates() {
        const string yaml = """
            title: NTLMv1 except approved service identities
            id: 1fc94d31-f86e-47c9-9563-3a9bdf1f1071
            status: test
            description: Detects NTLMv1 while excluding approved service identities.
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: 4624
                LmPackageName|contains: NTLM V1
              filter_service:
                TargetUserName|startswith: svc_
              condition: selection and not filter_service
            falsepositives:
              - Approved legacy systems
            tags:
              - attack.t1557
            level: medium
            """;
        SigmaCompilationResult compilation = SigmaRuleCompiler.CompileYaml(yaml);
        EventDetectionPlan plan = compilation.CompilePlan();
        EventObservation alice = Observe(4624, 1, "alice", "10.0.0.1", "NTLM V1");
        EventObservation service = Observe(4624, 2, "svc_legacy", "10.0.0.2", "NTLM V1");

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(new[] { alice, service }, plan));

        Assert.True(compilation.IsSupported);
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal("SIGMA-1fc94d31-f86e-47c9-9563-3a9bdf1f1071", finding.RuleId);
        Assert.Equal("Sigma", finding.SourceKind);
        Assert.Equal("test", finding.SourceStatus);
        Assert.Equal(alice.Identity, finding.EvidenceIdentities[0]);
    }

    [Fact]
    public void SigmaNullSelectionUsesIsNullAndPromotesGuaranteedEventIds() {
        const string yaml = """
            title: Missing optional authentication field
            id: 61616161-6161-4161-8161-616161616161
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: 4624
                OptionalField: null
              condition: selection
            level: medium
            """;
        SigmaCompilationResult compilation = SigmaRuleCompiler.CompileYaml(yaml);
        EventDetectionRuleDefinition definition = Assert.Single(compilation.Rules).Definition;
        EventObservation missing = Observe(4624, 1, "alice", "10.0.0.1");
        EventObject populatedSource = CreateEvent(4624, 2);
        populatedSource.Data["OptionalField"] = "present";
        EventObservation populated = EventObservation.Create(populatedSource);

        Assert.True(compilation.IsSupported);
        Assert.Equal(new[] { 4624 }, definition.EventIds);
        Assert.Contains("IsNull", definition.Predicate!.ToJson(), StringComparison.Ordinal);
        Assert.True(EventPredicateEvaluator.Matches(definition.Predicate, missing.Fields));
        Assert.False(EventPredicateEvaluator.Matches(definition.Predicate, populated.Fields));
        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(
            new[] { missing, populated },
            compilation.CompilePlan()));

        Assert.Equal(missing.Identity, finding.EvidenceIdentities[0]);
    }

    [Fact]
    public void SigmaFilesLoadAsOneCorrelationCompilationUnit() {
        const string baseYaml = """
            title: Failed logon
            id: 71717171-7171-4171-8171-717171717171
            name: failed_logon_file
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: 4625
              condition: selection
            """;
        const string correlationYaml = """
            title: Repeated failed logons
            id: 72727272-7272-4272-8272-727272727272
            correlation:
              type: event_count
              rules:
                - failed_logon_file
              group-by:
                - TargetUserName
              timespan: 5m
              condition:
                gte: 2
            level: high
            """;
        string directory = Path.Combine(Path.GetTempPath(), $"eventviewerx-sigma-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string basePath = Path.Combine(directory, "base.yml");
        string correlationPath = Path.Combine(directory, "correlation.yml");
        try {
            File.WriteAllText(basePath, baseYaml);
            File.WriteAllText(correlationPath, correlationYaml);

            SigmaCompilationResult compilation = SigmaRuleCompiler.Load(new[] { basePath, correlationPath });
            EventDetectionRuleDefinition definition = Assert.Single(compilation.Rules).Definition;

            Assert.True(compilation.IsSupported);
            Assert.Equal(EventDetectionRuleKind.Threshold, definition.Kind);
            Assert.Equal(new[] { 4625 }, definition.EventIds);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SigmaScalarSelectionMatchesTheRenderedEventMessage() {
        const string yaml = """
            title: Suspicious rendered message
            id: 55555555-5555-4555-8555-555555555555
            logsource:
              product: windows
              service: security
            detection:
              keywords:
                - suspicious phrase
              condition: keywords
            level: medium
            """;
        EventDetectionPlan plan = SigmaRuleCompiler.CompileYaml(yaml).CompilePlan();
        EventObject source = CreateEvent(1001, 1, "A suspicious phrase was rendered by the provider.");
        EventObservation observation = EventObservation.Create(source);

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(new[] { observation }, plan));

        Assert.Equal("A suspicious phrase was rendered by the provider.", observation.Fields["Message"]);
        Assert.Equal(observation.Identity, finding.EvidenceIdentities[0]);
    }

    [Fact]
    public void SigmaOrderedTemporalCorrelationUsesTheSharedBoundedEngine() {
        const string yaml = """
            title: Failed logon
            id: 11111111-1111-4111-8111-111111111111
            name: failed_logon
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: 4625
              condition: selection
            level: informational
            ---
            title: Successful logon
            id: 22222222-2222-4222-8222-222222222222
            name: successful_logon
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: 4624
              condition: selection
            level: informational
            ---
            title: Failed then successful logon
            id: 33333333-3333-4333-8333-333333333333
            correlation:
              type: temporal_ordered
              rules:
                - failed_logon
                - successful_logon
              group-by:
                - TargetUserName
              timespan: 10m
            level: high
            """;
        SigmaCompilationResult compilation = SigmaRuleCompiler.CompileYaml(yaml);
        EventDetectionPlan plan = compilation.CompilePlan();
        EventObservation[] observations = {
            Observe(4624, 1, "alice", "10.0.0.1"),
            Observe(4625, 2, "alice", "10.0.0.1"),
            Observe(4624, 3, "bob", "10.0.0.2"),
            Observe(4624, 4, "alice", "10.0.0.1", domain: "CONTOSO")
        };

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.True(compilation.IsSupported);
        Assert.Single(compilation.Rules);
        Assert.Equal(EventDetectionRuleKind.OrderedTemporal, plan.Rules[0].Kind);
        Assert.Equal(new long?[] { 2, 4 }, finding.Evidence.Select(static item => item.RecordId));
        Assert.Equal("alice", finding.Entities["TargetUserName"]);
    }

    [Fact]
    public void SigmaValueCountCorrelationCountsDistinctFields() {
        const string yaml = """
            title: Failed logon
            id: aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa
            name: failed_logon
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: 4625
              condition: selection
            ---
            title: Distributed failures
            id: bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb
            correlation:
              type: value_count
              rules:
                - failed_logon
              group-by:
                - TargetUserName
              timespan: 5m
              condition:
                field: IpAddress
                gte: 3
            level: medium
            """;
        EventDetectionPlan plan = SigmaRuleCompiler.CompileYaml(yaml).CompilePlan();
        EventObservation[] observations = {
            Observe(4625, 1, "alice", "10.0.0.1"),
            Observe(4625, 2, "alice", "10.0.0.1"),
            Observe(4625, 3, "alice", "10.0.0.2"),
            Observe(4625, 4, "alice", "10.0.0.3")
        };

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(EventDetectionRuleKind.DistinctValue, plan.Rules[0].Kind);
        Assert.Equal(3, finding.Evidence.Count);
    }

    [Fact]
    public void SigmaCorrelationHashIncludesResolvedBaseRuleContent() {
        const string template = """
            title: Failed logon
            id: aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa
            name: failed_logon
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: __EVENT_ID__
              condition: selection
            ---
            title: Repeated failures
            id: bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb
            correlation:
              type: event_count
              rules:
                - failed_logon
              group-by:
                - TargetUserName
              timespan: 5m
              condition:
                gte: 2
            level: medium
            """;

        EventDetectionRuleDefinition first = Assert.Single(
            SigmaRuleCompiler.CompileYaml(template.Replace("__EVENT_ID__", "4625", StringComparison.Ordinal)).Rules)
            .Definition;
        EventDetectionRuleDefinition second = Assert.Single(
            SigmaRuleCompiler.CompileYaml(template.Replace("__EVENT_ID__", "4776", StringComparison.Ordinal)).Rules)
            .Definition;

        Assert.Equal(first.SourceId, second.SourceId);
        Assert.NotEqual(first.SourceHash, second.SourceHash);
    }

    [Fact]
    public void ProcessCreationCategoryDoesNotSilentlyRestrictSecurityEventsToSysmon() {
        const string yaml = """
            title: Security process creation
            id: 44444444-4444-4444-8444-444444444444
            logsource:
              product: windows
              category: process_creation
            detection:
              selection:
                EventID: 4688
              condition: selection
            level: medium
            """;
        SigmaCompilationResult compilation = SigmaRuleCompiler.CompileYaml(yaml);
        EventDetectionPlan plan = compilation.CompilePlan();
        EventObservation observation = Observe(4688, 1, "alice", "10.0.0.1");

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(new[] { observation }, plan));

        Assert.True(compilation.IsSupported);
        Assert.Empty(plan.Rules[0].Channels);
        Assert.Equal(4688, finding.Evidence[0].EventId);
        Assert.Equal("Security", finding.Evidence[0].SourceLog);
    }

    [Fact]
    public void SigmaCategoryWithoutGuaranteedEventIdFailsClosed() {
        const string yaml = """
            title: Unbounded process creation category
            id: 45454545-4545-4545-8545-454545454545
            logsource:
              product: windows
              category: process_creation
            detection:
              selection:
                Image|endswith: \\powershell.exe
              condition: selection
            level: medium
            """;

        SigmaCompilationResult compilation = SigmaRuleCompiler.CompileYaml(yaml);

        Assert.False(compilation.IsSupported);
        Assert.Empty(compilation.Rules);
        SigmaDiagnostic diagnostic = Assert.Single(compilation.Diagnostics);
        Assert.Equal("EVXSIGMA023", diagnostic.Code);
        Assert.Equal(SigmaDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("explicit EventID", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedSigmaBehaviorProducesStructuredErrorsWithoutRules() {
        const string yaml = """
            title: Unsupported Linux base64 rule
            id: cccccccc-cccc-4ccc-8ccc-cccccccccccc
            logsource:
              product: linux
            detection:
              selection:
                CommandLine|base64: dGVzdA==
              condition: selection
            """;

        SigmaCompilationResult result = SigmaRuleCompiler.CompileYaml(yaml);

        Assert.False(result.IsSupported);
        Assert.Empty(result.Rules);
        SigmaDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(SigmaDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("EVXSIGMA122", diagnostic.Code);
        Assert.Throws<InvalidDataException>(() => result.CompilePlan());
    }

    [Fact]
    public void SigmaJsonSchemaProfileRejectsMissingRequiredLogSourceAndExposesVersionedSchemas() {
        const string yaml = """
            title: Missing log source
            id: eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee
            detection:
              selection:
                EventID: 4624
              condition: selection
            """;

        SigmaCompilationResult result = SigmaRuleCompiler.CompileYaml(yaml);

        Assert.False(result.IsSupported);
        SigmaDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EVXSIGMA004", diagnostic.Code);
        Assert.Equal("2.1.0", SigmaSchemaCatalog.SupportedSpecificationVersion);
        Assert.Contains("\"required\": [\"title\", \"logsource\", \"detection\"]", SigmaSchemaCatalog.GetSchema(false));
        Assert.Contains("temporal_ordered", SigmaSchemaCatalog.GetSchema(true));
    }

    [Fact]
    public void EquivalentNativeAndSigmaRulesRetainTheSameEvidenceIdentity() {
        const string yaml = """
            title: NTLMv1 authentication observed
            id: dddddddd-dddd-4ddd-8ddd-dddddddddddd
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: 4624
                LmPackageName: NTLM V1
              condition: selection
            level: medium
            """;
        EventObject source = CreateEvent(4624, 42);
        source.Data["LmPackageName"] = "NTLM V1";
        EventDetectionPlan nativePlan = EventDetectionPlan.Compile(
            EventDetectionCatalog.GetBuiltInRules().Where(static rule => rule.Definition.RuleId == "EVX-AUTH-0001"));
        EventDetectionPlan sigmaPlan = SigmaRuleCompiler.CompileYaml(yaml).CompilePlan();

        EventDetectionFinding native = Assert.Single(EventDetectionEngine.Evaluate(new[] { source }, nativePlan).Findings);
        EventDetectionFinding sigma = Assert.Single(EventDetectionEngine.Evaluate(new[] { source }, sigmaPlan).Findings);

        Assert.Equal(native.EvidenceIdentities, sigma.EvidenceIdentities);
        Assert.Equal(native.Severity, sigma.Severity);
        Assert.Equal(EventDetectionFindingStatus.Matched, sigma.Status);
    }

    private static EventObservation Observe(
        int eventId,
        long recordId,
        string account,
        string address,
        string? lmPackage = null,
        string domain = "EVOTEC") {

        EventObject source = CreateEvent(eventId, recordId);
        source.Data["TargetDomainName"] = domain;
        source.Data["TargetUserName"] = account;
        source.Data["IpAddress"] = address;
        if (lmPackage != null) {
            source.Data["LmPackageName"] = lmPackage;
        }
        DateTime time = source.TimeCreated;
        return EventObservation.Create(source, receivedTimeUtc: time, processedTimeUtc: time);
    }

    private static EventObject CreateEvent(int eventId, long recordId, string? message = null) {
        DateTime time = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc).AddMinutes(recordId);
        var metadata = new NativeEventMetadata(
            "Microsoft-Windows-Security-Auditing",
            providerId: null,
            eventId,
            qualifiers: null,
            level: 0,
            task: 0,
            opcode: 0,
            keywords: 0,
            time,
            recordId,
            activityId: null,
            relatedActivityId: null,
            processId: 1,
            threadId: 2,
            logName: "Security",
            machineName: "server01",
            userId: null,
            version: 1);
        if (message == null) {
            return new EventObject(metadata, queriedMachine: "collector01", containerLog: "Security");
        }
        var rendered = new NativeEventMessage(
            metadata,
            message,
            "Information",
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            bookmark: null,
            cultureName: "en-US",
            EventMessageRenderStatus.Rendered,
            renderErrorCode: 0);
        return new EventObject(rendered, queriedMachine: "collector01", containerLog: "Security");
    }
}
