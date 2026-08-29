using EventViewerX.Native;
using System.Security.Cryptography;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventDetection {
    [Fact]
    public void ObservationCombinesStableMetadataRawDataAndTypedFields() {
        DateTime eventTime = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
        DateTime received = eventTime.AddSeconds(2);
        DateTime processed = received.AddMilliseconds(25);
        EventObject source = CreateEvent(4624, eventTime, 41, "Security", "Microsoft-Windows-Security-Auditing");
        source.Data["LmPackageName"] = "NTLM V1";
        source.Data["SubjectDomainName"] = "EVOTEC";
        source.Data["SubjectUserName"] = "alice";
        EventTypeRecord typed = Assert.IsType<Rules.ActiveDirectory.ADUserLogonNTLMv1>(
            EventTypeCatalog.CreateEventRule(source, new[] { EventType.ActiveDirectoryAuthentication }));

        EventObservation observation = EventObservation.Create(source, typed, received, processed);

        Assert.Equal(EventCheckpointBoundaryIdentity.Create(source), observation.Identity);
        Assert.Equal("ADUserLogonNTLMv1", observation.TypeName);
        Assert.Equal("NTLM V1", observation.Fields["LmPackageName"]);
        Assert.Equal("EVOTEC\\alice", observation.Fields["Who"]);
        Assert.Equal(eventTime, observation.EventTimeUtc);
        Assert.Equal(received, observation.ReceivedTimeUtc);
        Assert.Equal(processed, observation.ProcessedTimeUtc);
        Assert.Same(source, observation.SourceEvent);
        Assert.Throws<ArgumentException>(() => EventObservation.Create(
            source,
            typed,
            received,
            received.AddTicks(-1)));
    }

    [Fact]
    public void BuiltInNtlmV1RuleProducesExplainableFinding() {
        EventObject source = CreateEvent(4624, Utc(10, 0), 42, "Security", "Microsoft-Windows-Security-Auditing");
        source.Data["LmPackageName"] = "NTLM V1";
        EventTypeRecord typed = Assert.IsType<Rules.ActiveDirectory.ADUserLogonNTLMv1>(
            EventTypeCatalog.CreateEventRule(source, new[] { EventType.ActiveDirectoryAuthentication }));
        EventObservation observation = EventObservation.Create(source, typed, Utc(10, 1), Utc(10, 1));
        EventDetectionPlan plan = EventDetectionPlan.Compile(EventDetectionCatalog.GetBuiltInRules());

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(new[] { observation }, plan));

        Assert.Equal("EVX-AUTH-0001", finding.RuleId);
        Assert.Equal(EventDetectionFindingStatus.Matched, finding.Status);
        Assert.Equal(EventDetectionSeverity.Medium, finding.Severity);
        Assert.Equal(new[] { observation.Identity }, finding.EvidenceIdentities);
        Assert.Contains("attack.t1557", finding.Tags);
        Assert.Equal("eventviewerx.authentication-modernization", finding.PackId);
        Assert.Equal("1.0.0", finding.PackVersion);
        Assert.Equal("Native", finding.SourceKind);
        Assert.Equal("MIT", finding.License);
        Assert.Equal(64, finding.SourceHash.Length);
        Assert.Null(finding.CompletenessDiagnostic);
    }

    [Fact]
    public void ThresholdRuleGroupsPrunesAndEmitsOnlyAtTheConfiguredCount() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            Rule("EVX-TEST-THRESHOLD", EventDetectionRuleKind.Threshold, threshold: 3, groupBy: "Account")
        });
        EventObservation[] observations = {
            Observe(1001, Utc(10, 0), 1, "alice"),
            Observe(1001, Utc(10, 1), 2, "bob"),
            Observe(1001, Utc(10, 2), 3, "alice"),
            Observe(1001, Utc(10, 7), 4, "alice"),
            Observe(1001, Utc(10, 8), 5, "alice"),
            Observe(1001, Utc(10, 9), 6, "alice")
        };

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(3, finding.Evidence.Count);
        Assert.Equal(Utc(10, 7), finding.StartTimeUtc);
        Assert.Equal(Utc(10, 9), finding.EndTimeUtc);
        Assert.Equal("alice", finding.Entities["Account"]);
    }

    [Fact]
    public void IndexedSelectorsRemainConjunctiveAfterCandidateLookup() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            Rule(
                "EVX-TEST-SELECTORS",
                eventIds: new[] { 1001 },
                channels: new[] { "Security" },
                providers: new[] { "Provider-A" })
        });
        EventObservation[] observations = {
            Observe(1002, Utc(10, 0), 1, "alice", "Security", "Provider-A"),
            Observe(1001, Utc(10, 1), 2, "alice", "System", "Provider-A"),
            Observe(1001, Utc(10, 2), 3, "alice", "Security", "Provider-B"),
            Observe(1001, Utc(10, 3), 4, "alice", "security", "provider-a")
        };

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(4, finding.Evidence[0].RecordId);
    }

    [Fact]
    public void CandidateLookupDoesNotScanRulesThatOnlyShareAChannel() {
        IEventDetectionRule[] rules = Enumerable.Range(0, 1000)
            .Select(index => Rule(
                $"EVX-TEST-INDEX-{index:D4}",
                eventIds: new[] { 10000 + index },
                channels: new[] { "Security" }))
            .ToArray();
        EventDetectionPlan plan = EventDetectionPlan.Compile(rules);
        EventObservation observation = Observe(10421, Utc(10, 0), 1, "alice");

        EventDetectionPlan.CompiledRule candidate = Assert.Single(plan.GetCandidates(observation));

        Assert.Equal("EVX-TEST-INDEX-0421", candidate.Definition.RuleId);
    }

    [Fact]
    public void DistinctValueCountsUniqueValuesWithinTheWindow() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition {
                RuleId = "EVX-TEST-DISTINCT",
                Title = "Distinct sources",
                Kind = EventDetectionRuleKind.DistinctValue,
                EventIds = new[] { 1001 },
                Threshold = 3,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account",
                DistinctBy = "SourceAddress"
            })
        });
        EventObservation first = Observe(1001, Utc(10, 0), 1, "alice");
        EventObservation duplicate = Observe(1001, Utc(10, 1), 2, "alice");
        EventObservation second = Observe(1001, Utc(10, 2), 3, "alice");
        EventObservation third = Observe(1001, Utc(10, 3), 4, "alice");
        first.SourceEvent.Data["SourceAddress"] = "10.0.0.1";
        duplicate.SourceEvent.Data["SourceAddress"] = "10.0.0.1";
        second.SourceEvent.Data["SourceAddress"] = "10.0.0.2";
        third.SourceEvent.Data["SourceAddress"] = "10.0.0.3";
        EventObservation[] observations = new[] { first, duplicate, second, third }
            .Select(item => EventObservation.Create(
                item.SourceEvent,
                receivedTimeUtc: item.ReceivedTimeUtc,
                processedTimeUtc: item.ProcessedTimeUtc))
            .ToArray();

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(3, finding.Evidence.Count);
        Assert.Equal("alice", finding.Entities["Account"]);
        Assert.Contains("distinct SourceAddress", finding.Explanation);
    }

    [Fact]
    public void StreamingThresholdKeepsOutOfOrderEvidenceChronologicalAtInclusiveBoundary() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            Rule("EVX-TEST-OUT-OF-ORDER", EventDetectionRuleKind.Threshold, threshold: 3, groupBy: "Account")
        });
        EventObservation newest = Observe(1001, Utc(10, 5), 3, "alice");
        EventObservation oldest = Observe(1001, Utc(10, 0), 1, "alice");
        EventObservation middle = Observe(1001, Utc(10, 4), 2, "alice");

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(
            new[] { newest, oldest, middle },
            plan));

        Assert.Equal(
            new[] { oldest.Identity, middle.Identity, newest.Identity },
            finding.EvidenceIdentities);
        Assert.Equal(TimeSpan.FromMinutes(5), finding.EndTimeUtc - finding.StartTimeUtc);
    }

    [Fact]
    public void TemporalRuleAcceptsStepsInAnyOrderButRetainsIndependentEvidence() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            TemporalRule("EVX-TEST-TEMPORAL", EventDetectionRuleKind.Temporal)
        });
        EventObservation[] observations = {
            Observe(1002, Utc(10, 1), 2, "alice"),
            Observe(1001, Utc(10, 2), 3, "alice")
        };

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(new long?[] { 2, 3 }, finding.Evidence.Select(static item => item.RecordId));
        Assert.Contains("all 2 temporal steps", finding.Explanation);
    }

    [Fact]
    public void OrderedTemporalRuleRequiresDeclaredOrderAndRestartsFromFirstStep() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            TemporalRule("EVX-TEST-ORDERED", EventDetectionRuleKind.OrderedTemporal)
        });
        EventObservation[] observations = {
            Observe(1002, Utc(10, 0), 1, "alice"),
            Observe(1001, Utc(10, 1), 2, "alice"),
            Observe(1001, Utc(10, 2), 3, "alice"),
            Observe(1002, Utc(10, 3), 4, "alice")
        };

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(new long?[] { 3, 4 }, finding.Evidence.Select(static item => item.RecordId));
        Assert.Contains("in order", finding.Explanation);
    }

    [Fact]
    public void TemporalIndexKeepsRuleEligibleWhenOneStepLeavesASelectorUnrestricted() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition {
                RuleId = "EVX-TEST-MIXED-SELECTORS",
                Title = "Mixed temporal selectors",
                Kind = EventDetectionRuleKind.OrderedTemporal,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account",
                Steps = new[] {
                    new EventDetectionStepDefinition { Name = "specific", EventIds = new[] { 1001 } },
                    new EventDetectionStepDefinition {
                        Name = "predicate-only",
                        Predicate = EventPredicate.Compare("Account", EventPredicateOperator.Equal, "alice")
                    }
                }
            })
        });
        EventObservation[] observations = {
            Observe(1001, Utc(10, 0), 1, "alice"),
            Observe(9001, Utc(10, 1), 2, "alice")
        };

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(new long?[] { 1, 2 }, finding.Evidence.Select(static item => item.RecordId));
    }

    [Fact]
    public void MaterializedEvaluationNormalizesNewestFirstInputForOrderedCorrelation() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            TemporalRule("EVX-TEST-MATERIALIZED-ORDER", EventDetectionRuleKind.OrderedTemporal)
        });
        EventObservation[] newestFirst = {
            Observe(1002, Utc(10, 1), 2, "alice"),
            Observe(1001, Utc(10, 0), 1, "alice")
        };

        EventDetectionExecutionResult execution = EventDetectionEngine.Evaluate(newestFirst, plan);
        EventDetectionFinding finding = Assert.Single(execution.Findings);

        Assert.Equal(new long?[] { 1, 2 }, finding.Evidence.Select(static item => item.RecordId));
    }

    [Fact]
    public void DistinctValueReportsMissingRequiredFieldsWithoutCountingThem() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition {
                RuleId = "EVX-TEST-DISTINCT-MISSING",
                Title = "Distinct sources require source data",
                Kind = EventDetectionRuleKind.DistinctValue,
                EventIds = new[] { 1001 },
                Threshold = 3,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account",
                DistinctBy = "SourceAddress"
            })
        });
        EventObservation[] observations = {
            WithField(Observe(1001, Utc(10, 0), 1, "alice"), "SourceAddress", "10.0.0.1"),
            Observe(1001, Utc(10, 1), 2, "alice"),
            WithField(Observe(1001, Utc(10, 2), 3, "alice"), "SourceAddress", "10.0.0.2"),
            WithField(Observe(1001, Utc(10, 3), 4, "alice"), "SourceAddress", "10.0.0.3")
        };

        EventDetectionFinding[] findings = EventDetectionEngine.Stream(observations, plan).ToArray();

        EventDetectionFinding incomplete = Assert.Single(
            findings,
            static finding => finding.Status == EventDetectionFindingStatus.Incomplete);
        EventDetectionFinding matched = Assert.Single(
            findings,
            static finding => finding.Status == EventDetectionFindingStatus.Matched);
        Assert.Contains("SourceAddress", incomplete.CompletenessDiagnostic, StringComparison.Ordinal);
        Assert.Equal(new long?[] { 1, 3, 4 }, matched.Evidence.Select(static item => item.RecordId));
    }

    [Fact]
    public void MaterializedDryRunStopsReadingAfterTheObservationBoundSentinel() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { Rule("EVX-TEST-BOUNDED-DRY-RUN") });
        int enumerated = 0;
        IEnumerable<EventObservation> Source() {
            while (true) {
                enumerated++;
                if (enumerated > 3) {
                    throw new InvalidOperationException("The dry run enumerated beyond its bound sentinel.");
                }
                yield return Observe(1001, Utc(10, enumerated), enumerated, "alice");
            }
        }

        EventDetectionExecutionResult execution = EventDetectionEngine.Evaluate(
            Source(),
            plan,
            new EventDetectionEngineOptions(maximumObservations: 2));

        Assert.Equal(3, enumerated);
        Assert.Equal(3, execution.ObservationCount);
        Assert.Single(execution.Findings, static finding =>
            finding.Status == EventDetectionFindingStatus.Incomplete &&
            finding.CompletenessDiagnostic!.Contains("MaximumObservations", StringComparison.Ordinal));
    }

    [Fact]
    public void TuningDisablesOverridesAndSuppressesWithoutMutatingSourceRules() {
        EventDetectionRule severityRule = Rule("EVX-TEST-SEVERITY", severity: EventDetectionSeverity.Low);
        EventDetectionRule disabledRule = Rule("EVX-TEST-DISABLED");
        EventDetectionRule thresholdRule = Rule(
            "EVX-TEST-TUNED-THRESHOLD",
            EventDetectionRuleKind.Threshold,
            threshold: 5,
            groupBy: "Account");
        EventDetectionPlan plan = EventDetectionPlan.Compile(
            new[] { severityRule, disabledRule, thresholdRule },
            new EventDetectionTuning(
                disabledRuleIds: new[] { disabledRule.Definition.RuleId },
                severityOverrides: new Dictionary<string, EventDetectionSeverity> {
                    [severityRule.Definition.RuleId] = EventDetectionSeverity.Critical
                },
                thresholdOverrides: new Dictionary<string, int> {
                    [thresholdRule.Definition.RuleId] = 2
                },
                suppressions: new[] {
                    new EventDetectionSuppression(
                        severityRule.Definition.RuleId,
                        EventPredicate.Compare("Account", EventPredicateOperator.Equal, "service"),
                        reason: "Approved test identity")
                }));
        EventObservation[] observations = {
            Observe(1001, Utc(10, 0), 1, "service"),
            Observe(1001, Utc(10, 1), 2, "alice"),
            Observe(1001, Utc(10, 2), 3, "alice")
        };

        EventDetectionFinding[] findings = EventDetectionEngine.Stream(observations, plan).ToArray();

        Assert.DoesNotContain(findings, static finding => finding.RuleId == "EVX-TEST-DISABLED");
        Assert.Equal(2, findings.Count(static finding =>
            finding.RuleId == "EVX-TEST-SEVERITY" && finding.Severity == EventDetectionSeverity.Critical));
        Assert.Single(findings, static finding =>
            finding.RuleId == "EVX-TEST-TUNED-THRESHOLD" && finding.Evidence.Count == 2);
        Assert.Equal(EventDetectionSeverity.Low, severityRule.Definition.Severity);
        Assert.Equal(5, thresholdRule.Definition.Threshold);
    }

    [Fact]
    public void ExecutionBoundsProduceOneExplicitIncompleteFinding() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            Rule("EVX-TEST-BOUNDS", EventDetectionRuleKind.Threshold, threshold: 2, groupBy: "Account")
        });
        EventObservation[] observations = {
            Observe(1001, Utc(10, 0), 1, "alice"),
            Observe(1001, Utc(10, 1), 2, "bob"),
            Observe(1001, Utc(10, 2), 3, "charlie")
        };
        var options = new EventDetectionEngineOptions(
            maximumObservations: 0,
            maximumGroups: 1,
            maximumStateObservations: 10);

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan, options));

        Assert.Equal("EVX-ENGINE-BOUNDS", finding.RuleId);
        Assert.Equal(EventDetectionFindingStatus.Incomplete, finding.Status);
        Assert.Contains("MaximumGroups", finding.CompletenessDiagnostic);
    }

    [Fact]
    public async Task AsyncStreamingMatchesSynchronousDetection() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { Rule("EVX-TEST-ASYNC") });
        EventObservation[] observations = {
            Observe(1001, Utc(10, 0), 1, "alice"),
            Observe(1001, Utc(10, 1), 2, "bob")
        };

        EventDetectionFinding[] synchronous = EventDetectionEngine.Stream(observations, plan).ToArray();
        var asynchronous = new List<EventDetectionFinding>();
        await foreach (EventDetectionFinding finding in EventDetectionEngine.StreamAsync(ToAsync(observations), plan)) {
            asynchronous.Add(finding);
        }

        Assert.Equal(
            synchronous.Select(static finding => (finding.RuleId, finding.EvidenceIdentities[0])),
            asynchronous.Select(static finding => (finding.RuleId, finding.EvidenceIdentities[0])));
    }

    [Fact]
    public void RawEventDryRunCompilesRequiredProjectionAndExplainPlan() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(EventDetectionCatalog.GetBuiltInRules());
        EventObject source = CreateEvent(4624, Utc(10, 0), 42, "Security", "Microsoft-Windows-Security-Auditing");
        source.Data["LmPackageName"] = "NTLM V1";

        EventDetectionExecutionResult result = EventDetectionEngine.Evaluate(new[] { source }, plan);
        EventDetectionPlanExplanation explanation = plan.Explain();

        Assert.Equal(1, result.ObservationCount);
        Assert.True(result.IsEvaluationComplete);
        Assert.False(result.IsComplete);
        Assert.Single(result.Findings, static finding => finding.RuleId == "EVX-AUTH-0001");
        Assert.Contains(EventType.ADUserLogonNTLMv1, explanation.RequiredEventTypes);
        Assert.Equal(44, explanation.RuleCount);
        Assert.Equal(4, explanation.StatefulRuleCount);
    }

    [Fact]
    public void DetectionPackRoundTripsAndRejectsTamperingOrWrongSignatureKey() {
        EventDetectionPack pack = EventDetectionCatalog.GetBuiltInPacks()[0];
        string json = pack.ToJson();
        EventDetectionPack restored = EventDetectionPack.ParseJson(json);
        using RSA signingKey = RSA.Create(2048);
        using RSA wrongKey = RSA.Create(2048);
        EventDetectionPack signed = pack.Sign(signingKey);

        Assert.True(restored.Validate().IsValid);
        Assert.Equal(EventDetectionPackSignatureStatus.Unsigned, restored.Validate().SignatureStatus);
        Assert.True(signed.Validate(signingKey, requireSignature: true).IsValid);
        Assert.False(signed.Validate(wrongKey, requireSignature: true).IsValid);
        Assert.Equal(EventDetectionPackSignatureStatus.Invalid, signed.Validate(wrongKey).SignatureStatus);

        string tamperedJson = json.Replace("Security event log cleared", "Security event log erased", StringComparison.Ordinal);
        EventDetectionPack tampered = EventDetectionPack.ParseJson(tamperedJson);
        EventDetectionPackValidationResult tamperedValidation = tampered.Validate();
        Assert.False(tamperedValidation.IsValid);
        Assert.False(tamperedValidation.ContentHashValid);
        Assert.Throws<InvalidDataException>(() => tampered.GetRules());
    }

    [Fact]
    public void DetectionPackFailsClosedForFutureEngineOrUnknownObservationSchema() {
        EventDetectionRuleDefinition rule = Rule("EVX-TEST-PACK-COMPATIBILITY").Definition;
        EventDetectionPack futureEngine = EventDetectionPack.Create(
            "eventviewerx.test.future-engine",
            "1.0.0",
            new[] { rule },
            minimumEngineVersion: "99.0.0");
        EventDetectionPack unknownSchema = EventDetectionPack.Create(
            "eventviewerx.test.future-schema",
            "1.0.0",
            new[] { rule },
            observationSchemaVersion: "2.0.0");

        EventDetectionPackValidationResult engineValidation = futureEngine.Validate();
        EventDetectionPackValidationResult schemaValidation = unknownSchema.Validate();

        Assert.False(engineValidation.IsValid);
        Assert.Contains(engineValidation.Diagnostics, static diagnostic =>
            diagnostic.Contains("engine 99.0.0", StringComparison.Ordinal));
        Assert.False(schemaValidation.IsValid);
        Assert.Contains(schemaValidation.Diagnostics, static diagnostic =>
            diagnostic.Contains("observation schema 2.0.0", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => futureEngine.GetRules());
        Assert.Throws<InvalidDataException>(() => unknownSchema.GetRules());
    }

    [Fact]
    public void PackComparisonAndCoverageExposeContentChangesBeforeEnablement() {
        EventDetectionPack previous = EventDetectionCatalog.GetBuiltInPacks()
            .Single(static pack => pack.PackId == "eventviewerx.authentication-modernization");
        EventDetectionRuleDefinition[] changedRules = previous.Rules.ToArray();
        changedRules[0].Severity = EventDetectionSeverity.High;
        var added = new EventDetectionRuleDefinition {
            RuleId = "EVX-AUTH-9999",
            Title = "New authentication rule",
            EventTypes = new[] { EventType.KerberosTicketFailure }
        };
        EventDetectionPack current = EventDetectionPack.Create(
            previous.PackId,
            "1.1.0",
            changedRules.Append(added),
            previous.Authors,
            previous.License,
            createdUtc: previous.CreatedUtc.AddDays(1));

        EventDetectionPackComparison comparison = previous.CompareTo(current);
        EventDetectionPackCoverage coverage = current.GetCoverage();

        Assert.True(comparison.HasChanges);
        Assert.Contains("EVX-AUTH-0001", comparison.ChangedRuleIds);
        Assert.Contains("EVX-AUTH-9999", comparison.AddedRuleIds);
        Assert.Empty(comparison.RemovedRuleIds);
        Assert.Contains(EventType.ADUserLogonNTLMv1, coverage.EventTypes);
        Assert.Contains(EventType.KerberosTicketFailure, coverage.EventTypes);
    }

    [Fact]
    public void FixtureApiComparesFindingOrderAndMultiplicity() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { Rule("EVX-TEST-FIXTURE") });
        var fixture = new EventDetectionFixture {
            Name = "two matches",
            Observations = new[] {
                Observe(1001, Utc(10, 0), 1, "alice"),
                Observe(1001, Utc(10, 1), 2, "bob")
            },
            ExpectedRuleIds = new[] { "EVX-TEST-FIXTURE", "EVX-TEST-FIXTURE" }
        };

        EventDetectionFixtureResult result = EventDetectionEngine.TestFixture(fixture, plan);

        Assert.True(result.IsMatch);
        Assert.Equal(2, result.Execution.ObservationCount);
        Assert.Equal(result.ExpectedRuleIds, result.ActualRuleIds);
    }

    [Fact]
    public void EveryBuiltInRuleShipsFourExecutableFixtureContracts() {
        IReadOnlyList<IEventDetectionRule> rules = EventDetectionCatalog.GetBuiltInRules();
        IReadOnlyList<EventDetectionFixture> fixtures = EventDetectionCatalog.GetBuiltInFixtures();
        IReadOnlyList<EventDetectionFixtureResult> results = EventDetectionCatalog.TestBuiltInFixtures();

        Assert.Equal(rules.Count * 4, fixtures.Count);
        foreach (IGrouping<string, EventDetectionFixture> ruleFixtures in fixtures.GroupBy(
                     static fixture => fixture.RuleId,
                     StringComparer.OrdinalIgnoreCase)) {
            Assert.Equal(4, ruleFixtures.Count());
            Assert.Equal(
                Enum.GetValues(typeof(EventDetectionFixtureKind)).Cast<EventDetectionFixtureKind>().OrderBy(static kind => kind),
                ruleFixtures.Select(static fixture => fixture.Kind).OrderBy(static kind => kind));
            Assert.All(ruleFixtures, static fixture => {
                Assert.False(string.IsNullOrWhiteSpace(fixture.PackId));
                Assert.False(string.IsNullOrWhiteSpace(fixture.Description));
                Assert.NotEmpty(fixture.Observations);
            });
        }
        Assert.All(results, static result => Assert.True(
            result.IsMatch,
            $"Fixture '{result.Name}' expected [{string.Join(", ", result.ExpectedRuleIds)}] but produced [{string.Join(", ", result.ActualRuleIds)}]."));
    }

    [Fact]
    public void PackCoveragePublishesAuditPolicyAndTargetRoleRequirements() {
        EventDetectionPack authentication = EventDetectionCatalog.GetBuiltInPacks()
            .Single(static pack => pack.PackId == "eventviewerx.authentication-modernization");

        EventDetectionPackCoverage coverage = authentication.GetCoverage();

        Assert.Contains(coverage.AuditPolicies, static requirement =>
            requirement.Name == "Audit Logon" &&
            requirement.AuditOutcomes.HasFlag(EventAuditOutcome.Success));
        Assert.Contains(coverage.AuditPolicies, static requirement =>
            requirement.Name == "Audit Logon" &&
            requirement.AuditOutcomes.HasFlag(EventAuditOutcome.Failure));
        Assert.Contains(coverage.TargetRoles, static requirement => requirement.Key == "target-role:domain-controller");
        Assert.Contains(coverage.Prerequisites, static requirement =>
            requirement.Kind == EventRequirementKind.Configuration &&
            requirement.Key == "configuration:ntds-ldap-interface-events-2");
    }

    [Fact]
    public void CandidateAndEstimatedStateByteBoundsAreExplicit() {
        EventObservation observation = Observe(1001, Utc(10, 0), 1, "alice");
        EventDetectionPlan candidatePlan = EventDetectionPlan.Compile(new[] {
            Rule("EVX-TEST-CANDIDATE-1"),
            Rule("EVX-TEST-CANDIDATE-2")
        });
        EventDetectionPlan statePlan = EventDetectionPlan.Compile(new[] {
            Rule("EVX-TEST-STATE-BYTES", EventDetectionRuleKind.Threshold, threshold: 2, groupBy: "Account")
        });

        EventDetectionFinding candidateBound = Assert.Single(EventDetectionEngine.Stream(
            new[] { observation },
            candidatePlan,
            new EventDetectionEngineOptions(maximumCandidateRules: 1)));
        EventDetectionFinding byteBound = Assert.Single(EventDetectionEngine.Stream(
            new[] { observation },
            statePlan,
            new EventDetectionEngineOptions(maximumStateBytes: 1)));

        Assert.Equal(EventDetectionFindingStatus.Incomplete, candidateBound.Status);
        Assert.Contains("MaximumCandidateRules", candidateBound.CompletenessDiagnostic);
        Assert.Equal(EventDetectionFindingStatus.Incomplete, byteBound.Status);
        Assert.Contains("MaximumStateBytes", byteBound.CompletenessDiagnostic);
    }

    [Fact]
    public void DetectionOptionsBuilderProducesValidatedImmutableSnapshots() {
        EventDetectionCoverage coverage = EventDetectionCoverage.Create(
            expectedTargets: new[] { "server01" },
            observedTargets: new[] { "server01" });

        EventDetectionEngineOptions options = new EventDetectionEngineOptionsBuilder()
            .WithMaximumObservations(42)
            .WithMaximumGroups(12)
            .WithMaximumStateObservations(128)
            .WithMaximumStateBytes(4096)
            .WithMaximumCandidateRules(9)
            .WithCoverage(coverage)
            .Build();

        Assert.Equal(42, options.MaximumObservations);
        Assert.Equal(12, options.MaximumGroups);
        Assert.Equal(128, options.MaximumStateObservations);
        Assert.Equal(4096, options.MaximumStateBytes);
        Assert.Equal(9, options.MaximumCandidateRules);
        Assert.NotSame(coverage, options.Coverage);
        Assert.True(options.Coverage!.IsComplete);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventDetectionEngineOptionsBuilder().WithMaximumGroups(0).Build());
    }

    [Fact]
    public void PlanAndTuningBuildersDetachCollectionsAndRoundTripJson() {
        var sourceDisabled = new List<string> { "EVX-TEST-DISABLED" };
        EventDetectionTuning tuning = new EventDetectionTuning(
            disabledRuleIds: sourceDisabled,
            severityOverrides: new Dictionary<string, EventDetectionSeverity> {
                ["EVX-TEST-BUILDER"] = EventDetectionSeverity.High
            });
        sourceDisabled.Add("EVX-TEST-LATE-MUTATION");
        EventDetectionPlan plan = new EventDetectionPlanBuilder()
            .AddRule(Rule("EVX-TEST-BUILDER"))
            .AddRule(Rule("EVX-TEST-DISABLED"))
            .WithTuning(tuning)
            .Build();
        string json = System.Text.Json.JsonSerializer.Serialize(tuning);
        EventDetectionTuning restored = System.Text.Json.JsonSerializer.Deserialize<EventDetectionTuning>(json)!;

        Assert.Single(tuning.DisabledRuleIds);
        Assert.Single(plan.Rules);
        Assert.Equal(EventDetectionSeverity.High, plan.Rules[0].Severity);
        Assert.Equal(tuning.DisabledRuleIds, restored.DisabledRuleIds);
        Assert.Equal(EventDetectionSeverity.High, restored.SeverityOverrides["EVX-TEST-BUILDER"]);
    }

    [Fact]
    public void AnalysisJsonContractsAreVersionedParseableAndAvoidDuplicateSourceObjects() {
        EventObservation observation = Observe(1001, Utc(10, 0), 1, "alice");
        EventDetectionPlan plan = new EventDetectionPlanBuilder()
            .AddRule(Rule("EVX-TEST-CONTRACT"))
            .Build();
        EventDetectionCoverage coverage = EventDetectionCoverage.Create(
            expectedEventIds: new[] { 1001 },
            observedEventIds: new[] { 1001 });
        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(
            new[] { observation },
            plan,
            new EventDetectionEngineOptions(coverage: coverage)));
        EventDetectionRuleTrace trace = Assert.Single(EventDetectionEngine.Explain(observation, plan, coverage));

        using System.Text.Json.JsonDocument observationJson = System.Text.Json.JsonDocument.Parse(
            EventAnalysisJson.Serialize(observation));
        using System.Text.Json.JsonDocument findingJson = System.Text.Json.JsonDocument.Parse(
            EventAnalysisJson.Serialize(finding));
        using System.Text.Json.JsonDocument planJson = System.Text.Json.JsonDocument.Parse(
            EventAnalysisJson.Serialize(plan));
        using System.Text.Json.JsonDocument traceJson = System.Text.Json.JsonDocument.Parse(
            EventAnalysisJson.Serialize(trace));

        Assert.Equal(1, observationJson.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(observationJson.RootElement.TryGetProperty("sourceEvent", out _));
        Assert.Equal(observation.Identity, findingJson.RootElement
            .GetProperty("evidenceIdentities")[0].GetString());
        Assert.False(findingJson.RootElement.TryGetProperty("evidence", out _));
        Assert.Equal(1, planJson.RootElement.GetProperty("ruleCount").GetInt32());
        Assert.Equal(observation.Identity, traceJson.RootElement.GetProperty("observationIdentity").GetString());
        Assert.All(EventAnalysisContractCatalog.GetContracts(), static contract => {
            Assert.Equal(EventAnalysisContractCatalog.CurrentSchemaVersion, contract.SchemaVersion);
            using System.Text.Json.JsonDocument schema = System.Text.Json.JsonDocument.Parse(contract.JsonSchema);
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                schema.RootElement.GetProperty("$schema").GetString());
            Assert.True(schema.RootElement.GetProperty("required").GetArrayLength() > 0);
        });
    }

    [Fact]
    public void TimelineKeepsThreeClocksEvidenceAndCanonicalPivots() {
        EventObservation observation = Observe(1001, Utc(10, 0), 1, "alice");
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { Rule("EVX-TEST-TIMELINE") });
        EventDetectionCoverage coverage = EventDetectionCoverage.Create(
            expectedTargets: new[] { "server01" },
            observedTargets: new[] { "server01" },
            expectedChannels: new[] { "Security" },
            observedChannels: new[] { "Security" },
            expectedEventIds: new[] { 1001 },
            observedEventIds: new[] { 1001 });
        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(
            new[] { observation },
            plan,
            new EventDetectionEngineOptions(coverage: coverage)));

        EventTimeline timeline = EventTimelineEngine.Create(
            new[] { observation },
            new[] { finding },
            new EventTimelineOptions {
                PivotKind = EventPivotKind.Account,
                PivotValue = "EVOTEC\\alice"
            });

        Assert.Equal(2, timeline.Entries.Count);
        Assert.Equal(EventTimelineEntryKind.Observation, timeline.Entries[0].Kind);
        Assert.Equal(EventTimelineEntryKind.Finding, timeline.Entries[1].Kind);
        Assert.Equal(observation.EventTimeUtc, timeline.Entries[0].EventTimeUtc);
        Assert.Equal(observation.ReceivedTimeUtc, timeline.Entries[0].ReceivedTimeUtc);
        Assert.Equal(observation.ProcessedTimeUtc, timeline.Entries[0].ProcessedTimeUtc);
        Assert.Equal(new[] { observation.Identity }, timeline.Entries[1].EvidenceIdentities);
        Assert.All(timeline.Entries, static entry => Assert.Contains(
            entry.Pivots,
            static pivot => pivot.Kind == EventPivotKind.Account && pivot.Value == "EVOTEC\\alice"));
    }

    [Fact]
    public void DetectionReportCarriesCoverageProvenanceCompletenessAndRendererSections() {
        EventObservation observation = Observe(1001, Utc(10, 0), 1, "alice");
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { Rule("EVX-TEST-REPORT") });
        EventDetectionCoverage coverage = EventDetectionCoverage.Create(
            expectedTargets: new[] { "server01" },
            observedTargets: new[] { "server01" },
            expectedChannels: new[] { "Security" },
            observedChannels: new[] { "Security" },
            expectedEventIds: new[] { 1001 },
            observedEventIds: new[] { 1001 });
        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(
            new[] { observation },
            plan,
            new EventDetectionEngineOptions(coverage: coverage)));
        EventDetectionPack pack = EventDetectionPack.Create(
            "eventviewerx.test-report",
            "1.0.0",
            new[] {
                new EventDetectionRuleDefinition {
                    RuleId = "EVX-TEST-REPORT-COVERAGE",
                    Title = "Report coverage rule",
                    EventIds = new[] { 1001 },
                    Channels = new[] { "Security" }
                }
            });

        EventDetectionReportSnapshot report = EventDetectionReportEngine.Create(
            new[] { observation },
            new[] { finding },
            new[] { pack },
            new EventDetectionReportOptions {
                Title = "Detection posture",
                QueryOwner = "unit-test-query",
                Limits = new[] { "MaximumObservations=100" },
                Coverage = coverage
            });

        Assert.True(report.IsComplete);
        Assert.False(report.UsedStorageHistory);
        Assert.Equal("unit-test-query", report.QueryOwner);
        Assert.Contains("server01", report.Targets);
        Assert.Contains("Security", report.Channels);
        EventDetectionPackHealth packHealth = Assert.Single(report.Packs);
        Assert.True(packHealth.HasRequiredDataCoverage);
        Assert.Empty(packHealth.MissingRequiredChannels);
        Assert.Equal(1, report.SeverityCounts[EventDetectionSeverity.Medium]);
        Assert.Contains(report.PresentationReport.Sections, static section => section.Name == "DetectionFinding");
        EventViewerX.Reporting.EventReportSection timeline = Assert.Single(
            report.PresentationReport.Sections,
            static section => section.Name == "IncidentTimeline");
        Assert.Equal(2, timeline.Rows.Count);
        Assert.Equal(3, report.PresentationReport.Rows.Count);
    }

    [Fact]
    public void DetectionReportIsIncompleteWhenRequiredTelemetryIsAbsent() {
        EventObservation observation = Observe(1001, Utc(10, 0), 1, "alice");
        EventDetectionPack pack = EventDetectionPack.Create(
            "eventviewerx.test-missing-coverage",
            "1.0.0",
            new[] {
                new EventDetectionRuleDefinition {
                    RuleId = "EVX-TEST-MISSING-COVERAGE",
                    Title = "Missing coverage rule",
                    EventIds = new[] { 2001 },
                    Channels = new[] { "System" }
                }
            });

        EventDetectionReportSnapshot report = EventDetectionReportEngine.Create(
            new[] { observation },
            Array.Empty<EventDetectionFinding>(),
            new[] { pack });

        Assert.False(report.IsComplete);
        EventDetectionPackHealth health = Assert.Single(report.Packs);
        Assert.False(health.HasRequiredDataCoverage);
        Assert.Equal(new[] { "System" }, health.MissingRequiredChannels);
    }

    [Fact]
    public void DetectionCoverageJsonRoundTripsAndRejectsFutureContracts() {
        EventDetectionCoverage coverage = EventDetectionCoverage.Create(
            expectedTargets: new[] { "server01" },
            observedTargets: new[] { "server01" },
            expectedChannels: new[] { "Security" },
            observedChannels: new[] { "Security" },
            expectedProviders: new[] { "Provider-A" },
            observedProviders: new[] { "Provider-A" },
            expectedEventIds: new[] { 4624 },
            observedEventIds: new[] { 4624 },
            expectedEventTypes: new[] { EventType.ADUserLogon },
            observedEventTypes: new[] { EventType.ADUserLogon });

        string json = coverage.ToJson();
        EventDetectionCoverage restored = EventDetectionCoverage.FromJson(json);

        Assert.True(restored.IsComplete);
        Assert.Equal(coverage.ExpectedTargets, restored.ExpectedTargets);
        Assert.Equal(coverage.ObservedEventIds, restored.ObservedEventIds);
        Assert.Equal(coverage.ExpectedEventTypes, restored.ExpectedEventTypes);
        string future = json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => EventDetectionCoverage.FromJson(future));
    }

    [Fact]
    public void ExplainTraceIdentifiesFailedConditionMatchAndUnavailableCoverage() {
        EventObservation observation = Observe(1001, Utc(10, 0), 1, "alice");
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            Rule("EVX-TRACE-MATCH", channels: new[] { "Security" }),
            Rule("EVX-TRACE-CHANNEL", channels: new[] { "System" })
        });
        EventDetectionCoverage complete = EventDetectionCoverage.Create(
            expectedChannels: new[] { "Security" },
            observedChannels: new[] { "Security" },
            expectedEventIds: new[] { 1001 },
            observedEventIds: new[] { 1001 });

        IReadOnlyList<EventDetectionRuleTrace> traces = EventDetectionEngine.Explain(observation, plan, complete);
        EventDetectionRuleTrace matched = Assert.Single(traces, static trace => trace.RuleId == "EVX-TRACE-MATCH");
        EventDetectionRuleTrace rejected = Assert.Single(traces, static trace => trace.RuleId == "EVX-TRACE-CHANNEL");
        EventDetectionRuleTrace unavailable = Assert.Single(
            EventDetectionEngine.Explain(observation, plan),
            static trace => trace.RuleId == "EVX-TRACE-MATCH");

        Assert.True(matched.Accepted);
        Assert.Contains("Matched all", matched.Outcome, StringComparison.Ordinal);
        Assert.False(rejected.Accepted);
        Assert.Contains("Rejected by Channel", rejected.Outcome, StringComparison.Ordinal);
        Assert.Contains(rejected.Conditions, static condition =>
            condition.Condition == "Channel" && !condition.Satisfied);
        Assert.Contains("Evidence unavailable", unavailable.Outcome, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanRejectsDuplicateIdsAndInvalidDefinitions() {
        EventDetectionRule first = Rule("EVX-TEST-DUPLICATE");
        EventDetectionRule second = Rule("evx-test-duplicate");

        Assert.Throws<InvalidDataException>(() => EventDetectionPlan.Compile(new[] { first, second }));
        Assert.Throws<InvalidDataException>(() => new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "EVX-TEST-INVALID",
            Title = "Invalid threshold",
            Kind = EventDetectionRuleKind.Threshold,
            Threshold = 1
        }));
    }

    private static EventDetectionRule Rule(
        string id,
        EventDetectionRuleKind kind = EventDetectionRuleKind.Stateless,
        int threshold = 1,
        string? groupBy = null,
        EventDetectionSeverity severity = EventDetectionSeverity.Medium,
        IReadOnlyList<int>? eventIds = null,
        IReadOnlyList<string>? channels = null,
        IReadOnlyList<string>? providers = null) {

        return new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = id,
            Version = "1.0.0",
            Title = id,
            Description = "Detection contract test.",
            Severity = severity,
            Confidence = 80,
            Kind = kind,
            EventIds = eventIds ?? new[] { 1001 },
            Channels = channels ?? Array.Empty<string>(),
            Providers = providers ?? Array.Empty<string>(),
            Threshold = threshold,
            Window = TimeSpan.FromMinutes(5),
            GroupBy = groupBy
        });
    }

    private static EventDetectionRule TemporalRule(string id, EventDetectionRuleKind kind) {
        return new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = id,
            Version = "1.0.0",
            Title = id,
            Description = "Temporal detection contract test.",
            Kind = kind,
            Window = TimeSpan.FromMinutes(5),
            GroupBy = "Account",
            Steps = new[] {
                new EventDetectionStepDefinition { Name = "first", EventIds = new[] { 1001 } },
                new EventDetectionStepDefinition { Name = "second", EventIds = new[] { 1002 } }
            }
        });
    }

    private static EventObservation Observe(
        int eventId,
        DateTime time,
        long recordId,
        string account,
        string channel = "Security",
        string provider = "Provider-A") {

        EventObject source = CreateEvent(eventId, time, recordId, channel, provider);
        source.Data["Account"] = account;
        source.Data["TargetDomainName"] = "EVOTEC";
        source.Data["TargetUserName"] = account;
        return EventObservation.Create(source, receivedTimeUtc: time, processedTimeUtc: time);
    }

    private static EventObservation WithField(
        EventObservation observation,
        string field,
        string value) {

        observation.SourceEvent.Data[field] = value;
        return EventObservation.Create(
            observation.SourceEvent,
            receivedTimeUtc: observation.ReceivedTimeUtc,
            processedTimeUtc: observation.ProcessedTimeUtc);
    }

    private static EventObject CreateEvent(
        int eventId,
        DateTime time,
        long recordId,
        string channel,
        string provider) {

        var metadata = new NativeEventMetadata(
            provider,
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
            channel,
            machineName: "server01",
            userId: null,
            version: 1);
        return new EventObject(metadata, queriedMachine: "collector01", containerLog: channel);
    }

    private static DateTime Utc(int hour, int minute) =>
        new(2026, 8, 28, hour, minute, 0, DateTimeKind.Utc);

    private static async IAsyncEnumerable<EventObservation> ToAsync(
        IEnumerable<EventObservation> observations) {

        foreach (EventObservation observation in observations) {
            await Task.Yield();
            yield return observation;
        }
    }
}
