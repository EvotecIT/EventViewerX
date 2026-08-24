using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventAnalysis {
    [Fact]
    public void NormalizationPreservesRawValuesAndCanonicalizesKnownDirectoryValues() {
        var row = CreateRow(1, new Dictionary<string, object?> {
            ["OperationType"] = "%%14674",
            ["ObjectGuid"] = "{A5299EBD-F3EA-D810-D4E0-BE199BD3C82F}",
            ["UserAccountControl"] = "0x202",
            ["AccountExpires"] = "0"
        });

        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(row);

        Assert.Equal("%%14674", normalized["OperationType"].RawValue);
        Assert.Equal("Value Added", normalized["OperationType"].Value);
        Assert.Equal(EventNormalizedValueKind.DirectoryOperation, normalized["OperationType"].Kind);
        Assert.Equal(new Guid("a5299ebd-f3ea-d810-d4e0-be199bd3c82f"), normalized["ObjectGuid"].Value);
        Assert.Contains("ACCOUNTDISABLE", Assert.IsType<string[]>(normalized["UserAccountControl"].Value));
        Assert.Contains("NORMAL_ACCOUNT", Assert.IsType<string[]>(normalized["UserAccountControl"].Value));
        Assert.Null(normalized["AccountExpires"].Value);
        Assert.Equal("Never", normalized["AccountExpires"].DisplayValue);
    }

    [Fact]
    public void UserAccountControlUnknownBitsRemainInCanonicalIdentity() {
        EventNormalizedValue first = EventValueNormalizationEngine.Normalize(CreateRow(
            1,
            new Dictionary<string, object?> { ["UserAccountControl"] = "0x02000202" }))["UserAccountControl"];
        EventNormalizedValue second = EventValueNormalizationEngine.Normalize(CreateRow(
            2,
            new Dictionary<string, object?> { ["UserAccountControl"] = "0x08000202" }))["UserAccountControl"];

        string[] firstFlags = Assert.IsType<string[]>(first.Value);
        string[] secondFlags = Assert.IsType<string[]>(second.Value);

        Assert.Contains("UNKNOWN_0x2000000", firstFlags);
        Assert.Contains("UNKNOWN_0x8000000", secondFlags);
        Assert.NotEqual(
            EventAggregationEngine.Canonicalize(firstFlags),
            EventAggregationEngine.Canonicalize(secondFlags));
        Assert.False(first.IsLossless);
        Assert.False(second.IsLossless);
    }

    [Fact]
    public void DistinguishedNameNormalizationPreservesSignificantWhitespaceAndEscapes() {
        EventNormalizedValue spaced = EventValueNormalizationEngine.Normalize(CreateRow(
            1,
            new Dictionary<string, object?> {
                ["ObjectDN"] = " CN = John  Doe , OU = Users "
            }))["ObjectDN"];
        EventNormalizedValue single = EventValueNormalizationEngine.Normalize(CreateRow(
            2,
            new Dictionary<string, object?> {
                ["ObjectDN"] = "CN=John Doe,OU=Users"
            }))["ObjectDN"];
        EventNormalizedValue escaped = EventValueNormalizationEngine.Normalize(CreateRow(
            3,
            new Dictionary<string, object?> {
                ["ObjectDN"] = @"CN=John\ ,OU=Users"
            }))["ObjectDN"];

        Assert.Equal("CN=John  Doe,OU=Users", spaced.Value);
        Assert.Equal(@"CN=John\ ,OU=Users", escaped.Value);
        Assert.NotEqual(
            EventAggregationEngine.Canonicalize(spaced.Value),
            EventAggregationEngine.Canonicalize(single.Value));
        Assert.Equal(2, spaced.NormalizerVersion);
    }

    [Fact]
    public void TextualUserAccountControlFlagsHaveCaseIndependentCanonicalIdentity() {
        EventNormalizedValue first = EventValueNormalizationEngine.Normalize(CreateRow(
            1,
            new Dictionary<string, object?> {
                ["UserAccountControl"] = "accountdisable, FUTURE_FLAG"
            }))["UserAccountControl"];
        EventNormalizedValue second = EventValueNormalizationEngine.Normalize(CreateRow(
            2,
            new Dictionary<string, object?> {
                ["UserAccountControl"] = "ACCOUNTDISABLE, future_flag"
            }))["UserAccountControl"];

        Assert.Equal(
            EventAggregationEngine.Canonicalize(first.Value),
            EventAggregationEngine.Canonicalize(second.Value));
        Assert.Equal(
            new[] { "ACCOUNTDISABLE", "FUTURE_FLAG" },
            Assert.IsType<string[]>(first.Value));
        Assert.Equal(EventNormalizationOutcome.Normalized, first.Outcome);
        Assert.Equal(EventNormalizationOutcome.Normalized, second.Outcome);
        Assert.Equal("accountdisable, FUTURE_FLAG", first.RawValue);
    }

    [Fact]
    public void NumericAndTextualUserAccountControlFlagsShareCanonicalOrder() {
        EventNormalizedValue numeric = EventValueNormalizationEngine.Normalize(CreateRow(
            1,
            new Dictionary<string, object?> {
                ["UserAccountControl"] = "3"
            }))["UserAccountControl"];
        EventNormalizedValue textual = EventValueNormalizationEngine.Normalize(CreateRow(
            2,
            new Dictionary<string, object?> {
                ["UserAccountControl"] = "SCRIPT, ACCOUNTDISABLE"
            }))["UserAccountControl"];

        Assert.Equal(
            EventAggregationEngine.Canonicalize(textual.Value),
            EventAggregationEngine.Canonicalize(numeric.Value));
        Assert.Equal(
            new[] { "ACCOUNTDISABLE", "SCRIPT" },
            Assert.IsType<string[]>(numeric.Value));
        Assert.Equal(2, numeric.NormalizerVersion);
        Assert.Equal(2, textual.NormalizerVersion);
    }

    [Fact]
    public void StructuredGroupPolicyLinkCollectionsRetainDistinctEvidence() {
        var firstLinks = new List<GroupPolicyLinks> {
            new() {
                DisplayName = "Baseline",
                Guid = "11111111-1111-1111-1111-111111111111",
                DistinguishedName = "CN={11111111-1111-1111-1111-111111111111},CN=Policies,DC=example,DC=com",
                IsEnabled = true
            }
        };
        var secondLinks = new List<GroupPolicyLinks> {
            new() {
                DisplayName = "Baseline",
                Guid = "22222222-2222-2222-2222-222222222222",
                DistinguishedName = "CN={22222222-2222-2222-2222-222222222222},CN=Policies,DC=example,DC=com",
                IsEnabled = true
            }
        };
        EventNormalizedValue first = EventValueNormalizationEngine.Normalize(CreateRow(
            1,
            new Dictionary<string, object?> { ["GroupPolicyLink"] = firstLinks }))["GroupPolicyLink"];
        EventNormalizedValue second = EventValueNormalizationEngine.Normalize(CreateRow(
            2,
            new Dictionary<string, object?> { ["GroupPolicyLink"] = secondLinks }))["GroupPolicyLink"];

        Assert.Equal(EventNormalizationOutcome.Unchanged, first.Outcome);
        Assert.Same(firstLinks, first.Value);
        Assert.NotEqual(
            EventAggregationEngine.Canonicalize(first.Value),
            EventAggregationEngine.Canonicalize(second.Value));
        Assert.Contains("11111111-1111-1111-1111-111111111111", first.DisplayValue, StringComparison.Ordinal);
        Assert.DoesNotContain("EventViewerX.GroupPolicyLinks", first.DisplayValue, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectoryOperationReportsOnlyExactCanonicalRepresentationAsUnchanged() {
        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(CreateRow(
                1,
                new Dictionary<string, object?> {
                    ["OperationType"] = " value added ",
                    ["ActionDetail"] = "Value Deleted"
                }));

        Assert.Equal("Value Added", normalized["OperationType"].Value);
        Assert.Equal(EventNormalizationOutcome.Normalized, normalized["OperationType"].Outcome);
        Assert.Equal(" value added ", normalized["OperationType"].RawValue);
        Assert.Equal("Value Deleted", normalized["ActionDetail"].Value);
        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized["ActionDetail"].Outcome);
    }

    [Fact]
    public void TypedDirectoryOperationLabelsRemainValidIdentityEvidence() {
        EventReportRow row = CreateRow(
            1,
            new Dictionary<string, object?> {
                ["OperationType"] = "Organizational Unit Created"
            });
        row.Type = nameof(EventType.ADOrganizationalUnitChangeDetailed);
        EventValueNormalizationEngine.Populate(row);

        EventNormalizedValue operation = row.NormalizedValues["OperationType"];

        Assert.Equal("Organizational Unit Created", operation.Value);
        Assert.Equal(EventNormalizationOutcome.Unchanged, operation.Outcome);
        Assert.Empty(operation.Warnings);
        Assert.Equal("identity", operation.Normalizer);
    }

    [Fact]
    public void MalformedKnownValuesRemainVisibleWithTypedDiagnostics() {
        var row = CreateRow(1, new Dictionary<string, object?> {
            ["ObjectGuid"] = "not-a-guid",
            ["AccountExpires"] = "not-a-filetime"
        });

        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(row);

        Assert.Equal("not-a-guid", normalized["ObjectGuid"].RawValue);
        Assert.Equal(EventNormalizationOutcome.Malformed, normalized["ObjectGuid"].Outcome);
        Assert.NotEmpty(normalized["ObjectGuid"].Warnings);
        Assert.Equal("not-a-filetime", normalized["AccountExpires"].RawValue);
        Assert.Equal(EventNormalizationOutcome.Malformed, normalized["AccountExpires"].Outcome);
    }

    [Fact]
    public void AbsentOptionalIdentityValuesRemainUnchanged() {
        var row = CreateRow(1, new Dictionary<string, object?> {
            ["ObjectGuid"] = null,
            ["ActorSid"] = string.Empty
        });

        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(row);

        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized["ObjectGuid"].Outcome);
        Assert.Null(normalized["ObjectGuid"].Value);
        Assert.Empty(normalized["ObjectGuid"].Warnings);
        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized["ActorSid"].Outcome);
        Assert.Equal(string.Empty, normalized["ActorSid"].Value);
        Assert.Empty(normalized["ActorSid"].Warnings);
    }

    [Fact]
    public void ActiveDirectoryGeneralizedTimeUsesItsOwnUtcContract() {
        var row = CreateRow(1, new Dictionary<string, object?> {
            ["WhenCreated"] = "20260823101530.125Z",
            ["AttributeLDAPDisplayName"] = "whenChanged",
            ["AttributeValue"] = "20260823121530,5+0200"
        });

        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(row);

        Assert.Equal(
            new DateTime(2026, 8, 23, 10, 15, 30, 125, DateTimeKind.Utc),
            normalized["WhenCreated"].Value);
        Assert.Equal(
            new DateTime(2026, 8, 23, 10, 15, 30, 500, DateTimeKind.Utc),
            normalized["AttributeValue"].Value);
        Assert.Equal("active-directory-generalized-time", normalized["WhenCreated"].Normalizer);
        Assert.Equal(EventNormalizationOutcome.Normalized, normalized["AttributeValue"].Outcome);
    }

    [Fact]
    public void TypedGeneralizedTimeOffsetsReportNormalizationToUtc() {
        var row = CreateRow(1, new Dictionary<string, object?> {
            ["WhenCreated"] = new DateTimeOffset(2026, 8, 23, 12, 15, 30, TimeSpan.FromHours(2))
        });

        EventNormalizedValue normalized = EventValueNormalizationEngine.Normalize(row)["WhenCreated"];

        Assert.Equal(new DateTime(2026, 8, 23, 10, 15, 30, DateTimeKind.Utc), normalized.Value);
        Assert.Equal(EventNormalizationOutcome.Normalized, normalized.Outcome);
    }

    [Fact]
    public void MissingDirectoryAndGeneralizedTimeValuesRemainUnchanged() {
        EventReportRow row = CreateRow(
            1,
            new Dictionary<string, object?> {
                ["OperationType"] = null,
                ["WhenCreated"] = " "
            });
        row.Type = nameof(EventType.GroupPolicyDirectoryAudit);
        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(row);

        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized["OperationType"].Outcome);
        Assert.Null(normalized["OperationType"].Value);
        Assert.Equal("identity", normalized["OperationType"].Normalizer);
        Assert.Empty(normalized["OperationType"].Warnings);
        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized["WhenCreated"].Outcome);
        Assert.Equal(" ", normalized["WhenCreated"].Value);
        Assert.Equal("identity", normalized["WhenCreated"].Normalizer);
        Assert.Empty(normalized["WhenCreated"].Warnings);
    }

    [Fact]
    public void MultiValueCanonicalCasingIsIndependentOfInputOrder() {
        EventNormalizedValue forward = EventValueNormalizationEngine.Normalize(CreateRow(
            1,
            new Dictionary<string, object?> {
                ["Privileges"] = new[] { "reader", "Admin", "admin" }
            }))["Privileges"];
        EventNormalizedValue reverse = EventValueNormalizationEngine.Normalize(CreateRow(
            2,
            new Dictionary<string, object?> {
                ["Privileges"] = new[] { "admin", "Admin", "reader" }
            }))["Privileges"];

        Assert.Equal(Assert.IsType<string[]>(forward.Value), Assert.IsType<string[]>(reverse.Value));
        Assert.Equal(forward.DisplayValue, reverse.DisplayValue);
    }

    [Fact]
    public void MalformedActiveDirectoryGeneralizedTimeRetainsTypedEvidence() {
        var row = CreateRow(1, new Dictionary<string, object?> {
            ["WhenChanged"] = "not-generalized-time"
        });

        EventNormalizedValue value = EventValueNormalizationEngine.Normalize(row)["WhenChanged"];

        Assert.Equal("not-generalized-time", value.RawValue);
        Assert.Equal(EventNormalizationOutcome.Malformed, value.Outcome);
        Assert.Equal("active-directory-generalized-time", value.Normalizer);
        Assert.NotEmpty(value.Warnings);
    }

    [Fact]
    public void GeneralizedTimeOverflowRetainsMalformedEvidence() {
        EventNormalizedValue value = EventValueNormalizationEngine.Normalize(CreateRow(
            1,
            new Dictionary<string, object?> {
                ["WhenCreated"] = "99991231235959.9-1400"
            }))["WhenCreated"];

        Assert.Equal("99991231235959.9-1400", value.RawValue);
        Assert.Equal(EventNormalizationOutcome.Malformed, value.Outcome);
        Assert.NotEmpty(value.Warnings);
    }

    [Fact]
    public void PasswordLastSetZeroRetainsPasswordChangeRequiredState() {
        var row = CreateRow(1, new Dictionary<string, object?> {
            ["PwdLastSet"] = "0",
            ["AccountExpires"] = "0"
        });

        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(row);

        Assert.Equal("PasswordChangeRequired", normalized["PwdLastSet"].Value);
        Assert.Equal("Must change password at next logon", normalized["PwdLastSet"].DisplayValue);
        Assert.Equal(EventNormalizedValueKind.DirectorySentinel, normalized["PwdLastSet"].Kind);
        Assert.Null(normalized["AccountExpires"].Value);
        Assert.Equal("Never", normalized["AccountExpires"].DisplayValue);
    }

    [Fact]
    public void TypedActiveDirectoryFileTimeReportsUtcConversion() {
        DateTime utc = new(2026, 8, 23, 10, 15, 30, DateTimeKind.Utc);
        DateTime local = DateTime.SpecifyKind(utc, DateTimeKind.Local);
        DateTimeOffset offset = new(2026, 8, 23, 12, 15, 30, TimeSpan.FromHours(2));
        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(CreateRow(
                1,
                new Dictionary<string, object?> {
                    ["LastLogon"] = local,
                    ["LastLogonTimestamp"] = utc,
                    ["BadPasswordTime"] = offset
                }));

        Assert.Equal(local.ToUniversalTime(), normalized["LastLogon"].Value);
        Assert.Equal(EventNormalizationOutcome.Normalized, normalized["LastLogon"].Outcome);
        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized["LastLogonTimestamp"].Outcome);
        Assert.Equal(offset.UtcDateTime, normalized["BadPasswordTime"].Value);
        Assert.Equal(EventNormalizationOutcome.Normalized, normalized["BadPasswordTime"].Outcome);
        Assert.Equal(offset, normalized["BadPasswordTime"].RawValue);
    }

    [Fact]
    public void MissingActiveDirectoryFileTimeValuesRemainUnchanged() {
        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(CreateRow(
                1,
                new Dictionary<string, object?> {
                    ["LastLogon"] = null,
                    ["AccountExpires"] = " "
                }));

        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized["LastLogon"].Outcome);
        Assert.Null(normalized["LastLogon"].Value);
        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized["AccountExpires"].Outcome);
        Assert.Equal(" ", normalized["AccountExpires"].Value);
        Assert.Equal("identity", normalized["LastLogon"].Normalizer);
        Assert.Equal("identity", normalized["AccountExpires"].Normalizer);
    }

    [Fact]
    public void ActiveDirectoryGeneralizedTimeValidatesOffsetsAndReportsFractionLoss() {
        var row = CreateRow(1, new Dictionary<string, object?> {
            ["WhenCreated"] = "20260823101530.123456789Z",
            ["WhenChanged"] = "20260823101530+0060"
        });

        IReadOnlyDictionary<string, EventNormalizedValue> normalized =
            EventValueNormalizationEngine.Normalize(row);

        Assert.Equal(
            new DateTime(2026, 8, 23, 10, 15, 30, DateTimeKind.Utc).AddTicks(1234567),
            normalized["WhenCreated"].Value);
        Assert.False(normalized["WhenCreated"].IsLossless);
        Assert.NotEmpty(normalized["WhenCreated"].Warnings);
        Assert.Equal(EventNormalizationOutcome.Malformed, normalized["WhenChanged"].Outcome);
    }

    [Fact]
    public void TransportAndSemanticGroupingRetainEveryObservation() {
        DateTime timestamp = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        EventReportRow direct = CreateRow(17, new Dictionary<string, object?> {
            ["OperationCorrelationId"] = "3a92a1b9-8f10-4e92-bfaf-c048c29276c8"
        }, timestamp, source: "dc1.ad.evotec.xyz", collector: "dc1.ad.evotec.xyz", container: "Security");
        EventReportRow forwarded = CreateRow(17, direct.Values, timestamp,
            source: "dc1.ad.evotec.xyz", collector: "wec1.ad.evotec.xyz", container: "ForwardedEvents");
        EventReportRow peer = CreateRow(22, direct.Values, timestamp.AddSeconds(2),
            source: "dc2.ad.evotec.xyz", collector: "dc2.ad.evotec.xyz", container: "Security");
        direct.EventId = 5136;
        direct.Type = "GroupPolicyValueChanged";
        forwarded.EventId = 5136;
        forwarded.Type = "GroupPolicyValueChanged";
        peer.EventId = 5137;
        peer.Type = "GroupPolicyValueApplied";

        EventOccurrenceResult transport = EventOccurrenceEngine.Group(
            new[] { peer, forwarded, direct },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Transport });
        EventOccurrenceResult semantic = EventOccurrenceEngine.Group(
            new[] { peer, forwarded, direct },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Semantic, Window = TimeSpan.FromSeconds(10) });

        Assert.True(transport.IsComplete);
        Assert.Equal(2, transport.Groups.Count);
        Assert.Contains(transport.Groups, static group => group.ObservationCount == 2);
        Assert.Equal(2, semantic.Groups.Count);
        EventOccurrenceGroup occurrence = semantic.Groups.Single(static group => group.ObservationCount == 2);
        Assert.Equal("causal-identifier", occurrence.PolicyName);
        Assert.Equal(5, occurrence.PolicyVersion);
        Assert.Same(direct, occurrence.Representative);
    }

    [Fact]
    public void SemanticGroupingDoesNotGuessFromTimeAndDimensions() {
        DateTime timestamp = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        EventReportRow first = CreateRow(1, new Dictionary<string, object?> { ["UserAffected"] = "alice" }, timestamp);
        EventReportRow second = CreateRow(2, new Dictionary<string, object?> { ["UserAffected"] = "alice" }, timestamp.AddSeconds(1));

        EventOccurrenceResult result = EventOccurrenceEngine.Group(
            new[] { second, first },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Semantic });

        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Groups.Count);
        Assert.All(result.Groups, static group => Assert.Equal(1, group.ObservationCount));
    }

    [Fact]
    public void SemanticGroupingUsesSystemActivityIdentifiersAcrossRelatedStages() {
        Guid activityId = Guid.Parse("7e2abf19-c7d2-40a9-8ec9-a5c7168e3c06");
        EventReportRow created = CreateRow(1, new Dictionary<string, object?>());
        EventReportRow completed = CreateRow(2, new Dictionary<string, object?>());
        created.EventId = 4698;
        created.Type = "ScheduledTaskCreated";
        created.ActivityId = activityId;
        completed.EventId = 4702;
        completed.Type = "ScheduledTaskUpdated";
        completed.ActivityId = Guid.Parse("4ce61f98-73d6-4e2e-bf3c-3390e20e100d");
        completed.RelatedActivityId = activityId;

        EventOccurrenceResult result = EventOccurrenceEngine.Group(
            new[] { completed, created },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Semantic });

        EventOccurrenceGroup group = Assert.Single(result.Groups);
        Assert.Equal(2, group.ObservationCount);
        Assert.Equal("causal-identifier", group.PolicyName);
    }

    [Fact]
    public void SemanticGroupingUnionsBothActivityIdentifiersAcrossCausalChains() {
        DateTime timestamp = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        Guid parentActivity = Guid.Parse("7e2abf19-c7d2-40a9-8ec9-a5c7168e3c06");
        Guid childActivity = Guid.Parse("4ce61f98-73d6-4e2e-bf3c-3390e20e100d");
        EventReportRow parent = CreateRow(1, new Dictionary<string, object?>(), timestamp);
        EventReportRow child = CreateRow(2, new Dictionary<string, object?>(), timestamp.AddSeconds(2));
        EventReportRow grandchild = CreateRow(3, new Dictionary<string, object?>(), timestamp.AddSeconds(4));
        parent.ActivityId = parentActivity;
        child.ActivityId = childActivity;
        child.RelatedActivityId = parentActivity;
        grandchild.RelatedActivityId = childActivity;

        EventOccurrenceGroup group = Assert.Single(EventOccurrenceEngine.Group(
            new[] { grandchild, parent, child },
            new EventOccurrenceOptions {
                Mode = EventDuplicateMode.Semantic,
                Window = TimeSpan.FromSeconds(10)
            }).Groups);

        Assert.Equal(3, group.ObservationCount);
        Assert.Equal(5, group.PolicyVersion);
    }

    [Fact]
    public void SemanticCausalChainsRemainBoundedByTheOccurrenceWindow() {
        DateTime timestamp = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        Guid parentActivity = Guid.Parse("7e2abf19-c7d2-40a9-8ec9-a5c7168e3c06");
        Guid childActivity = Guid.Parse("4ce61f98-73d6-4e2e-bf3c-3390e20e100d");
        EventReportRow parent = CreateRow(1, new Dictionary<string, object?>(), timestamp);
        EventReportRow child = CreateRow(2, new Dictionary<string, object?>(), timestamp.AddSeconds(9));
        EventReportRow grandchild = CreateRow(3, new Dictionary<string, object?>(), timestamp.AddSeconds(18));
        parent.ActivityId = parentActivity;
        child.ActivityId = childActivity;
        child.RelatedActivityId = parentActivity;
        grandchild.RelatedActivityId = childActivity;

        EventOccurrenceResult result = EventOccurrenceEngine.Group(
            new[] { grandchild, child, parent },
            new EventOccurrenceOptions {
                Mode = EventDuplicateMode.Semantic,
                Window = TimeSpan.FromSeconds(10)
            });

        Assert.Equal(2, result.Groups.Count);
        Assert.Contains(result.Groups, static group => group.ObservationCount == 2);
        Assert.Contains(result.Groups, static group => group.ObservationCount == 1);
    }

    [Fact]
    public void SemanticGroupingTreatsPayloadActivityAliasesAsOneCausalFamily() {
        const string activityId = "7e2abf19-c7d2-40a9-8ec9-a5c7168e3c06";
        EventReportRow parent = CreateRow(1, new Dictionary<string, object?> { ["ActivityID"] = activityId });
        EventReportRow child = CreateRow(2, new Dictionary<string, object?> { ["RelatedActivityId"] = activityId });

        EventOccurrenceGroup group = Assert.Single(EventOccurrenceEngine.Group(
            new[] { child, parent },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Semantic }).Groups);

        Assert.Equal(2, group.ObservationCount);
    }

    [Fact]
    public void GenericPayloadActivityIdentifiersRemainVisibleAndGroupable() {
        const string activityId = "7e2abf19-c7d2-40a9-8ec9-a5c7168e3c06";
        EventReportRow first = CreateRow(1, new Dictionary<string, object?> {
            ["ActivityId"] = activityId,
            ["ActivityId_ProviderField"] = "existing"
        });
        EventReportRow second = CreateRow(2, new Dictionary<string, object?> {
            ["RelatedActivityId"] = activityId
        });
        first.Type = "Generic";
        second.Type = "Generic";

        IReadOnlyDictionary<string, object?> projected = EventReportJsonProjection.Project(first);
        EventOccurrenceGroup group = Assert.Single(EventOccurrenceEngine.Group(
            new[] { second, first },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Semantic }).Groups);

        Assert.Null(projected[nameof(EventReportRow.ActivityId)]);
        Assert.Equal("existing", projected["ActivityId_ProviderField"]);
        Assert.Equal(activityId, projected["ActivityId_ProviderField2"]?.ToString());
        Assert.Equal(2, group.ObservationCount);
    }

    [Fact]
    public void SemanticGroupingNamespacesPayloadIdentifiersByProducerAndSource() {
        EventReportRow first = CreateRow(1, new Dictionary<string, object?> { ["BatchId"] = "1" });
        EventReportRow otherProvider = CreateRow(2, new Dictionary<string, object?> { ["BatchId"] = "1" });
        EventReportRow otherSource = CreateRow(
            3,
            new Dictionary<string, object?> { ["BatchId"] = "1" },
            source: "dc2.ad.evotec.xyz",
            collector: "dc2.ad.evotec.xyz");
        otherProvider.Provider = "Contoso-Synthetic-Provider";

        EventOccurrenceResult result = EventOccurrenceEngine.Group(
            new[] { first, otherProvider, otherSource },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Semantic });

        Assert.Equal(3, result.Groups.Count);
        Assert.All(result.Groups, static group => Assert.Equal(1, group.ObservationCount));
    }

    [Theory]
    [InlineData(EventDuplicateMode.None)]
    [InlineData(EventDuplicateMode.Transport)]
    public void UnkeyedOccurrenceIdentitiesAreIndependentOfInputOrder(EventDuplicateMode mode) {
        EventReportRow first = CreateRow(1, new Dictionary<string, object?> { ["Value"] = "one" });
        EventReportRow second = CreateRow(2, new Dictionary<string, object?> { ["Value"] = "two" });
        first.RecordId = null;
        second.RecordId = null;

        string[] forward = EventOccurrenceEngine.Group(
                new[] { first, second },
                new EventOccurrenceOptions { Mode = mode })
            .Groups.Select(static group => group.Identity).OrderBy(static value => value).ToArray();
        string[] reverse = EventOccurrenceEngine.Group(
                new[] { second, first },
                new EventOccurrenceOptions { Mode = mode })
            .Groups.Select(static group => group.Identity).OrderBy(static value => value).ToArray();

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void UnkeyedOccurrenceFingerprintsRetainShadowedDomainFieldsAcrossInputOrder() {
        EventReportRow first = CreateRow(1, new Dictionary<string, object?> { ["Message"] = "domain-a" });
        EventReportRow second = CreateRow(2, new Dictionary<string, object?> { ["Message"] = "domain-b" });
        first.RecordId = null;
        second.RecordId = null;
        first.Message = "shared-native-message";
        second.Message = "shared-native-message";

        EventOccurrenceResult forward = EventOccurrenceEngine.Group(
            new[] { first, second },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Transport });
        EventOccurrenceResult reverse = EventOccurrenceEngine.Group(
            new[] { second, first },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Transport });

        Assert.Equal(
            forward.Groups.Single(group => ReferenceEquals(group.Representative, first)).Identity,
            reverse.Groups.Single(group => ReferenceEquals(group.Representative, first)).Identity);
        Assert.Equal(
            forward.Groups.Single(group => ReferenceEquals(group.Representative, second)).Identity,
            reverse.Groups.Single(group => ReferenceEquals(group.Representative, second)).Identity);
        Assert.NotEqual(forward.Groups[0].Identity, forward.Groups[1].Identity);
    }

    [Fact]
    public void ManagedAggregationUsesCanonicalKeysAndTypedMeasures() {
        EventReportRow[] rows = {
            CreateRow(1, new Dictionary<string, object?> { ["Who"] = "Alice", ["IpAddress"] = "10.0.0.1" }),
            CreateRow(2, new Dictionary<string, object?> { ["Who"] = "alice", ["IpAddress"] = "10.0.0.2" }),
            CreateRow(3, new Dictionary<string, object?> { ["Who"] = "Bob", ["IpAddress"] = "10.0.0.2" })
        };
        var definition = new EventAggregationDefinition {
            GroupBy = new[] { "Who" },
            Measures = new[] {
                new EventAggregationMeasure { Operation = EventAggregationOperation.Count, OutputName = "Events" },
                new EventAggregationMeasure {
                    Operation = EventAggregationOperation.DistinctCount,
                    Field = "IpAddress",
                    OutputName = "Sources"
                }
            }
        };

        EventAggregationResult result = EventAggregationEngine.Aggregate(
            rows,
            definition,
            EventAggregationInputCompleteness.Complete);

        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Rows.Count);
        EventAggregationRow alice = result.Rows.Single(row => string.Equals(
            row.Group["Who"]?.ToString(), "Alice", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2L, alice.Measures["Events"]);
        Assert.Equal(2L, alice.Measures["Sources"]);
    }

    [Fact]
    public void ManagedAggregationDisplayValuesAreIndependentOfInputOrder() {
        EventReportRow upper = CreateRow(1, new Dictionary<string, object?> { ["Who"] = "Alice" });
        EventReportRow lower = CreateRow(2, new Dictionary<string, object?> { ["Who"] = "alice" });
        var definition = new EventAggregationDefinition { GroupBy = new[] { "Who" } };

        object? forward = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { lower, upper }, definition).Rows).Group["Who"];
        object? reverse = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { upper, lower }, definition).Rows).Group["Who"];

        Assert.Equal("Alice", forward);
        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void ManagedAggregationComparesCollectionDisplayValuesByContent() {
        EventReportRow upper = CreateRow(1, new Dictionary<string, object?> {
            ["Privileges"] = new[] { "ALICE" }
        });
        EventReportRow lower = CreateRow(2, new Dictionary<string, object?> {
            ["Privileges"] = new[] { "alice" }
        });
        var definition = new EventAggregationDefinition { GroupBy = new[] { "Privileges" } };

        object? forward = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { lower, upper }, definition).Rows).Group["Privileges"];
        object? reverse = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { upper, lower }, definition).Rows).Group["Privileges"];

        Assert.Equal(new[] { "ALICE" }, Assert.IsType<string[]>(forward));
        Assert.Equal(
            EventAggregationEngine.Canonicalize(forward),
            EventAggregationEngine.Canonicalize(reverse));
    }

    [Fact]
    public void ManagedAggregationCanonicalizesDictionariesIndependentOfInsertionOrder() {
        var forward = new Dictionary<string, object?> {
            ["B"] = 2,
            ["A"] = "one"
        };
        var reverse = new Dictionary<string, object?> {
            ["A"] = "one",
            ["B"] = 2
        };
        EventReportRow first = CreateRow(1, new Dictionary<string, object?> { ["Payload"] = forward });
        EventReportRow second = CreateRow(2, new Dictionary<string, object?> { ["Payload"] = reverse });
        var definition = new EventAggregationDefinition { GroupBy = new[] { "Payload" } };

        EventAggregationRow group = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { first, second }, definition).Rows);

        Assert.Equal(2L, group.Measures["Count"]);
        Assert.Equal(
            EventAggregationEngine.Canonicalize(forward),
            EventAggregationEngine.Canonicalize(reverse));
    }

    [Fact]
    public void ManagedAggregationNormalizesExternallyConstructedRows() {
        EventReportRow numeric = CreateRow(1, new Dictionary<string, object?>());
        EventReportRow textual = CreateRow(2, new Dictionary<string, object?>());
        numeric.Values = new Dictionary<string, object?> { ["UserAccountControl"] = "512" };
        textual.Values = new Dictionary<string, object?> { ["UserAccountControl"] = "NORMAL_ACCOUNT" };
        numeric.NormalizedValues = new Dictionary<string, EventNormalizedValue>();
        textual.NormalizedValues = new Dictionary<string, EventNormalizedValue>();
        var definition = new EventAggregationDefinition { GroupBy = new[] { "UserAccountControl" } };

        EventAggregationRow group = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { numeric, textual }, definition).Rows);

        Assert.Equal(new[] { "NORMAL_ACCOUNT" }, Assert.IsType<string[]>(group.Group["UserAccountControl"]));
        Assert.Equal(2L, group.Measures["Count"]);
    }

    [Fact]
    public void JsonProjectionPreservesProviderFieldsThatUseTheMetadataEnvelopeName() {
        EventReportRow row = CreateRow(1, new Dictionary<string, object?> {
            ["_EventViewerX"] = "provider-value",
            ["_EventViewerX_ProviderField"] = "existing-value"
        });

        IReadOnlyDictionary<string, object?> projected = EventReportJsonProjection.Project(row);

        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(projected["_EventViewerX"]);
        Assert.Equal("existing-value", projected["_EventViewerX_ProviderField"]);
        Assert.Equal("provider-value", projected["_EventViewerX_ProviderField2"]);
    }

    [Fact]
    public void PerBucketAggregationDoesNotAllocateHiddenGlobalDistinctState() {
        DateTime firstDay = new(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc);
        EventReportRow[] rows = Enumerable.Range(0, 120).Select(index => CreateRow(
            index + 1,
            new Dictionary<string, object?> {
                ["Who"] = "Alice",
                ["Value"] = "value-" + index
            },
            firstDay.AddDays(index / 60).AddMinutes(index % 60))).ToArray();
        var definition = new EventAggregationDefinition {
            GroupBy = new[] { "Who" },
            Bucket = EventAggregationBucket.Day,
            Measures = new[] {
                new EventAggregationMeasure {
                    Operation = EventAggregationOperation.DistinctCount,
                    Field = "Value",
                    OutputName = "Values"
                }
            },
            MaximumDistinctValues = 100
        };

        EventAggregationResult result = EventAggregationEngine.Aggregate(rows, definition);

        Assert.True(result.AggregationComplete);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, static row => Assert.Equal(60L, row.Measures["Values"]));
    }

    [Fact]
    public void GlobalRankingStateRetainsOnlyTheRankingMeasure() {
        DateTime firstDay = new(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc);
        EventReportRow[] rows = Enumerable.Range(0, 120).Select(index => CreateRow(
            index + 1,
            new Dictionary<string, object?> {
                ["Who"] = "Alice",
                ["Value"] = "value-" + index
            },
            firstDay.AddDays(index / 60).AddMinutes(index % 60))).ToArray();
        var definition = new EventAggregationDefinition {
            GroupBy = new[] { "Who" },
            Bucket = EventAggregationBucket.Day,
            Measures = new[] {
                new EventAggregationMeasure {
                    Operation = EventAggregationOperation.Count,
                    OutputName = "Events"
                },
                new EventAggregationMeasure {
                    Operation = EventAggregationOperation.DistinctCount,
                    Field = "Value",
                    OutputName = "Values"
                }
            },
            Top = 1,
            TopScope = EventAggregationTopScope.GlobalGroup,
            RankingMeasure = "Events",
            MaximumDistinctValues = 100
        };

        EventAggregationResult result = EventAggregationEngine.Aggregate(rows, definition);

        Assert.True(result.AggregationComplete);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, static row => Assert.Equal(60L, row.Measures["Values"]));
    }

    [Fact]
    public void BucketedGlobalRateRankingUsesTheFullSelectedInterval() {
        DateTime firstDay = new(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc);
        EventReportRow[] rows = {
            CreateRow(1, new Dictionary<string, object?> { ["Who"] = "Alice" }, firstDay.AddHours(1)),
            CreateRow(2, new Dictionary<string, object?> { ["Who"] = "Alice" }, firstDay.AddDays(1).AddHours(1)),
            CreateRow(3, new Dictionary<string, object?> { ["Who"] = "Bob" }, firstDay.AddHours(2)),
            CreateRow(4, new Dictionary<string, object?> { ["Who"] = "Bob" }, firstDay.AddHours(3)),
            CreateRow(5, new Dictionary<string, object?> { ["Who"] = "Bob" }, firstDay.AddDays(1).AddHours(2))
        };
        var definition = new EventAggregationDefinition {
            GroupBy = new[] { "Who" },
            Bucket = EventAggregationBucket.Day,
            Measures = new[] {
                new EventAggregationMeasure {
                    Operation = EventAggregationOperation.Rate,
                    OutputName = "PerHour",
                    RateUnit = TimeSpan.FromHours(1)
                }
            },
            Top = 1,
            TopScope = EventAggregationTopScope.GlobalGroup,
            RankingMeasure = "PerHour"
        };

        EventAggregationResult result = EventAggregationEngine.Aggregate(rows, definition);

        Assert.True(result.AggregationComplete);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, static row => Assert.Equal("Bob", row.Group["Who"]));
    }

    [Fact]
    public void ManagedAggregationResolvesCommonPredicateAliases() {
        EventReportRow row = CreateRow(1, new Dictionary<string, object?>());

        EventAggregationRow result = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { row },
            new EventAggregationDefinition { GroupBy = new[] { "ProviderName" } }).Rows);

        Assert.Equal(row.Provider, result.Group["ProviderName"]);
    }

    [Fact]
    public void ManagedAggregationPreservesDeclaredFieldsThatShadowCommonAliases() {
        EventReportRow typed = CreateRow(1, new Dictionary<string, object?> {
            ["ProviderName"] = "Declared Provider"
        });
        typed.Type = "CustomDefinition";
        EventValueNormalizationEngine.Populate(typed);
        EventReportRow generic = CreateRow(2, new Dictionary<string, object?> {
            ["ProviderName"] = "Generic Payload Provider"
        });
        generic.Type = "Generic";
        EventValueNormalizationEngine.Populate(generic);

        EventAggregationRow typedResult = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { typed },
            new EventAggregationDefinition { GroupBy = new[] { "ProviderName" } }).Rows);
        EventAggregationRow genericResult = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { generic },
            new EventAggregationDefinition { GroupBy = new[] { "ProviderName" } }).Rows);

        Assert.Equal("Declared Provider", typedResult.Group["ProviderName"]);
        Assert.Equal(generic.Provider, genericResult.Group["ProviderName"]);
    }

    [Fact]
    public void ManagedAggregationFailsClosedAtDeterministicBounds() {
        EventReportRow[] rows = Enumerable.Range(0, 3)
            .Select(index => CreateRow(index + 1, new Dictionary<string, object?> { ["Who"] = "user" + index }))
            .ToArray();
        var definition = new EventAggregationDefinition {
            GroupBy = new[] { "Who" },
            MaximumGroups = 2
        };

        EventAggregationResult forward = EventAggregationEngine.Aggregate(rows, definition);
        EventAggregationResult reverse = EventAggregationEngine.Aggregate(Enumerable.Reverse(rows), definition);

        Assert.False(forward.AggregationComplete);
        Assert.False(reverse.AggregationComplete);
        Assert.Empty(forward.Rows);
        Assert.Empty(reverse.Rows);
        Assert.Equal(forward.Diagnostic, reverse.Diagnostic);
    }

    [Fact]
    public void GlobalRankingStatesShareTheMaximumGroupBudget() {
        EventAggregationResult result = EventAggregationEngine.Aggregate(
            new[] {
                CreateRow(
                    1,
                    new Dictionary<string, object?> { ["Who"] = "Alice" },
                    new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc))
            },
            new EventAggregationDefinition {
                GroupBy = new[] { "Who" },
                Bucket = EventAggregationBucket.Day,
                Top = 1,
                TopScope = EventAggregationTopScope.GlobalGroup,
                MaximumGroups = 1
            });

        Assert.False(result.AggregationComplete);
        Assert.Empty(result.Rows);
        Assert.Equal(1, result.InputRows);
        Assert.Contains("MaximumGroups", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void DailyRateUsesElapsedUtcAcrossDaylightSavingTransition() {
        string zoneId = OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Warsaw";
        var definition = new EventAggregationDefinition {
            Bucket = EventAggregationBucket.Day,
            TimeZoneId = zoneId,
            Measures = new[] {
                new EventAggregationMeasure {
                    Operation = EventAggregationOperation.Rate,
                    OutputName = "PerHour",
                    RateUnit = TimeSpan.FromHours(1)
                }
            }
        };
        EventReportRow row = CreateRow(
            1,
            new Dictionary<string, object?>(),
            new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc));

        EventAggregationResult result = EventAggregationEngine.Aggregate(
            new[] { row },
            definition,
            EventAggregationInputCompleteness.Complete);

        EventAggregationRow bucket = Assert.Single(result.Rows);
        Assert.Equal(TimeSpan.FromHours(23), bucket.BucketEndUtc - bucket.BucketStartUtc);
        Assert.Equal(1d / 23d, Assert.IsType<double>(bucket.Measures["PerHour"]), precision: 10);
    }

    [Theory]
    [InlineData("2026-10-25T00:30:00Z")]
    [InlineData("2026-10-25T01:30:00Z")]
    public void RepeatedAutumnHoursRemainDistinctOneHourBuckets(string timestamp) {
        string zoneId = OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Warsaw";
        EventAggregationRow bucket = Assert.Single(EventAggregationEngine.Aggregate(
            new[] { CreateRow(1, new Dictionary<string, object?>(), DateTime.Parse(
                timestamp,
                null,
                System.Globalization.DateTimeStyles.AdjustToUniversal)) },
            new EventAggregationDefinition {
                Bucket = EventAggregationBucket.Hour,
                TimeZoneId = zoneId,
                Measures = new[] {
                    new EventAggregationMeasure {
                        Operation = EventAggregationOperation.Rate,
                        OutputName = "PerHour",
                        RateUnit = TimeSpan.FromHours(1)
                    }
                }
            }).Rows);

        Assert.Equal(TimeSpan.FromHours(1), bucket.BucketEndUtc - bucket.BucketStartUtc);
        Assert.Equal(1d, Assert.IsType<double>(bucket.Measures["PerHour"]), precision: 10);
    }

    [Theory]
    [InlineData(EventType.ScheduledTaskActivity, 4698, 4699, 4700, 4701, 4702)]
    [InlineData(EventType.FirewallRuleActivity, 4946, 4947, 4948)]
    [InlineData(EventType.DefenderSecurity, 1116, 1117, 5007)]
    public void SecurityMonitoringCompositesOwnTheExpectedEventIds(
        EventType type,
        params int[] expectedIds) {

        int[] actual = EventTypeCatalog.GetDefinition(type).Sources
            .SelectMany(static source => source.EventIds)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

        Assert.Equal(expectedIds.OrderBy(static id => id), actual);
    }

    [Fact]
    public void SecurityMonitoringLeavesDeclareRequiredAuditOrChannelEvidence() {
        foreach (EventType type in new[] {
                     EventType.ScheduledTaskCreated,
                     EventType.ScheduledTaskDeleted,
                     EventType.ScheduledTaskEnabled,
                     EventType.ScheduledTaskDisabled,
                     EventType.ScheduledTaskUpdated,
                     EventType.FirewallRuleAdded,
                     EventType.FirewallRuleChange,
                     EventType.FirewallRuleDeleted,
                     EventType.DefenderThreatDetected,
                     EventType.DefenderThreatAction,
                     EventType.DefenderConfigurationChanged
                 }) {
            EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(type);
            Assert.Contains(requirement.Prerequisites, prerequisite =>
                prerequisite.Kind == EventRequirementKind.EventChannel);
            if (!type.ToString().StartsWith("Defender", StringComparison.Ordinal)) {
                Assert.Contains(requirement.Prerequisites, prerequisite =>
                    prerequisite.Kind == EventRequirementKind.AuditPolicy);
            }
        }
    }

    [Fact]
    public void AuthenticationHealthPresetIncludesOnlyExplicitWeakAuthenticationSignals() {
        EventMonitoringPresetDefinition preset = EventMonitoringPresetCatalog.Get(
            EventMonitoringPreset.AuthenticationHealth);
        EventType[] leaves = EventTypeCatalog.Expand(preset.Types).ToArray();

        Assert.Contains(EventType.ADUserLogonNTLMv1, leaves);
        Assert.Contains(EventType.KerberosTGTRequest, leaves);
        Assert.Contains(EventType.KerberosServiceTicket, leaves);
        Assert.Contains(EventType.ADLdapBindingSummary, leaves);
        Assert.Contains(EventType.ADLdapBindingDetails, leaves);
        Assert.NotNull(preset.Predicate);
        EventPredicate predicate = EventPredicateBuilder.ForTypes(preset.Types).Normalize(preset.Predicate!);
        Assert.False(EventPredicateEvaluator.Matches(predicate, new Dictionary<string, object?> {
            ["TypeName"] = nameof(EventType.ADLdapBindingSummary),
            ["SimpleBindsWithoutTls"] = 0,
            ["NegotiateBindsWithoutSigning"] = 0
        }));
        Assert.True(EventPredicateEvaluator.Matches(predicate, new Dictionary<string, object?> {
            ["TypeName"] = nameof(EventType.ADLdapBindingSummary),
            ["SimpleBindsWithoutTls"] = 1,
            ["NegotiateBindsWithoutSigning"] = 0
        }));
    }

    [Fact]
    public void OccurrenceReportSummarizesWithoutDiscardingSourceObservations() {
        DateTime timestamp = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        Guid activityId = Guid.NewGuid();
        Guid relatedActivityId = Guid.NewGuid();
        EventReportRow direct = CreateRow(17, new Dictionary<string, object?>(), timestamp);
        direct.ActivityId = activityId;
        direct.RelatedActivityId = relatedActivityId;
        EventReportRow forwarded = CreateRow(17, new Dictionary<string, object?>(), timestamp,
            collector: "wec1.ad.evotec.xyz", container: "ForwardedEvents");
        forwarded.ActivityId = activityId;
        forwarded.RelatedActivityId = relatedActivityId;
        EventOccurrenceResult occurrences = EventOccurrenceEngine.Group(
            new[] { forwarded, direct },
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Transport });

        EventReport report = EventOccurrenceReportFactory.Create(occurrences);

        EventOccurrenceGroup group = Assert.Single(occurrences.Groups);
        Assert.Equal(2, group.Observations.Count);
        EventReportRow row = Assert.Single(report.Rows);
        Assert.Equal(2, row.Values["ObservationCount"]);
        Assert.Contains("17", Assert.IsType<string>(row.Values["RecordIds"]));
        Assert.Equal(activityId, row.ActivityId);
        Assert.Equal(relatedActivityId, row.RelatedActivityId);
    }

    [Fact]
    public void OccurrenceReportsAndRepresentativesRetainSourceCompletenessAndDomainFields() {
        EventReportRow row = CreateRow(17, new Dictionary<string, object?> { ["Who"] = "Alice" });
        var schema = new EventReportSectionSchema {
            Name = row.Type,
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                new EventReportColumnSchema {
                    Name = "Who",
                    ValueTypeName = EventReportColumnSchema.GetStableTypeName(typeof(string))
                }
            }
        };
        EventReport source = EventReportEngine.CreateStored(
            new[] { row },
            new[] { schema },
            coverage: new[] {
                new EventReportCoverage {
                    MachineName = "dc2.ad.evotec.xyz",
                    LogName = "Security",
                    Succeeded = false,
                    Status = "AccessDenied"
                }
            },
            eventsScanned: 25,
            scanLimitReached: true);
        EventOccurrenceResult occurrences = EventOccurrenceEngine.Group(
            source.Rows,
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Transport });

        EventReport summary = EventOccurrenceReportFactory.Create(occurrences, source);
        EventReport representatives = EventOccurrenceReportFactory.CreateRepresentatives(occurrences, source);
        EventAggregationResult aggregation = EventAggregationEngine.Aggregate(
            representatives,
            new EventAggregationDefinition { GroupBy = new[] { "Who" } });

        Assert.True(summary.ScanLimitReached);
        Assert.Equal(25, summary.EventsScanned);
        Assert.False(Assert.Single(summary.Coverage).Succeeded);
        Assert.True(representatives.ScanLimitReached);
        Assert.Equal("Alice", Assert.Single(aggregation.Rows).Group["Who"]);
        Assert.Equal(EventAggregationInputCompleteness.Incomplete, aggregation.InputCompleteness);
    }

    [Fact]
    public void OccurrenceRepresentativesComposeGroupingAndSourceDiagnosticsIntoAggregation() {
        EventReportRow[] rows = {
            CreateRow(1, new Dictionary<string, object?> { ["Who"] = "Alice" }),
            CreateRow(2, new Dictionary<string, object?> { ["Who"] = "Bob" })
        };
        var schema = new EventReportSectionSchema {
            Name = rows[0].Type,
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                new EventReportColumnSchema {
                    Name = "Who",
                    ValueTypeName = EventReportColumnSchema.GetStableTypeName(typeof(string))
                }
            }
        };
        EventReport source = EventReportEngine.CreateStored(
            rows,
            new[] { schema },
            coverage: new[] {
                new EventReportCoverage {
                    MachineName = "dc2.ad.evotec.xyz",
                    LogName = "Security",
                    Succeeded = false,
                    Status = "Timeout"
                }
            },
            scanLimitReached: true,
            completenessDiagnostic: "The remote source query timed out.");
        EventOccurrenceResult occurrences = EventOccurrenceEngine.Group(
            source.Rows,
            new EventOccurrenceOptions {
                Mode = EventDuplicateMode.Transport,
                MaximumObservations = 1
            });

        EventReport summary = EventOccurrenceReportFactory.Create(occurrences, source);
        EventReport representatives = EventOccurrenceReportFactory.CreateRepresentatives(occurrences, source);
        EventAggregationResult aggregation = EventAggregationEngine.Aggregate(
            representatives,
            new EventAggregationDefinition { GroupBy = new[] { "Who" } });

        Assert.Contains("MaximumObservations", summary.CompletenessDiagnostic, StringComparison.Ordinal);
        Assert.Contains("remote source query timed out", summary.CompletenessDiagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MaximumObservations", aggregation.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("remote source query timed out", aggregation.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregationChartsKeepNullAndLiteralNullSeriesDistinct() {
        DateTime day = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        EventReportRow missing = CreateRow(1, new Dictionary<string, object?> { ["Who"] = null }, day);
        EventReportRow literal = CreateRow(2, new Dictionary<string, object?> { ["Who"] = "(null)" }, day);
        EventAggregationResult result = EventAggregationEngine.Aggregate(
            new[] { missing, literal },
            new EventAggregationDefinition {
                GroupBy = new[] { "Who" },
                GroupNulls = EventAggregationNullPolicy.Include,
                Bucket = EventAggregationBucket.Day
            });

        EventAggregationChartData chart = Assert.IsType<EventAggregationChartData>(
            EventAggregationChartProjection.Create(result));

        Assert.Equal(2, chart.Series.Count);
        Assert.Equal(2, chart.Series.Select(static series => series.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("EventViewerX aggregation", EventAggregationHtmlRenderer.Render(result), StringComparison.Ordinal);
    }

    [Fact]
    public void PerBucketTopChartsExposeOmittedGroupsAsGapsRatherThanZero() {
        DateTime day = new(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc);
        EventReportRow[] rows = {
            CreateRow(1, new Dictionary<string, object?> { ["Who"] = "Alice" }, day),
            CreateRow(2, new Dictionary<string, object?> { ["Who"] = "Alice" }, day.AddMinutes(1)),
            CreateRow(3, new Dictionary<string, object?> { ["Who"] = "Bob" }, day.AddMinutes(2)),
            CreateRow(4, new Dictionary<string, object?> { ["Who"] = "Alice" }, day.AddDays(1)),
            CreateRow(5, new Dictionary<string, object?> { ["Who"] = "Bob" }, day.AddDays(1).AddMinutes(1)),
            CreateRow(6, new Dictionary<string, object?> { ["Who"] = "Bob" }, day.AddDays(1).AddMinutes(2))
        };
        EventAggregationResult result = EventAggregationEngine.Aggregate(
            rows,
            new EventAggregationDefinition {
                GroupBy = new[] { "Who" },
                Bucket = EventAggregationBucket.Day,
                Top = 1,
                TopScope = EventAggregationTopScope.PerBucket
            });

        EventAggregationChartData chart = Assert.IsType<EventAggregationChartData>(
            EventAggregationChartProjection.Create(result));
        string html = EventAggregationHtmlRenderer.Render(result);

        Assert.Equal(2, chart.Series.Count);
        Assert.All(chart.Series, static series => Assert.Contains(series.Points, static point => !point.HasValue));
        Assert.Contains("gaps shown explicitly", html, StringComparison.Ordinal);
        Assert.Contains("the value is a gap, not zero", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregationChartsAndExplorerFormatCanonicalMultiValueDimensions() {
        EventAggregationResult result = EventAggregationEngine.Aggregate(
            new[] {
                CreateRow(1, new Dictionary<string, object?> {
                    ["Privileges"] = new[] { "SeBackupPrivilege", "SeRestorePrivilege" }
                })
            },
            new EventAggregationDefinition {
                GroupBy = new[] { "Privileges" },
                Bucket = EventAggregationBucket.Day
            });

        EventAggregationChartData chart = Assert.IsType<EventAggregationChartData>(
            EventAggregationChartProjection.Create(result));
        string html = EventAggregationHtmlRenderer.Render(result);

        Assert.Contains("SeBackupPrivilege", Assert.Single(chart.Series).Name, StringComparison.Ordinal);
        Assert.Contains("SeRestorePrivilege", chart.Series[0].Name, StringComparison.Ordinal);
        Assert.DoesNotContain("System.String[]", chart.Series[0].Name, StringComparison.Ordinal);
        Assert.Contains("SeBackupPrivilege, SeRestorePrivilege", html, StringComparison.Ordinal);
        Assert.DoesNotContain("System.String[]", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyAggregationReportEmitsCompletenessMetadataRow() {
        EventAggregationResult bounded = EventAggregationEngine.Aggregate(
            new[] {
                CreateRow(1, new Dictionary<string, object?> { ["Who"] = "Alice" }),
                CreateRow(2, new Dictionary<string, object?> { ["Who"] = "Bob" })
            },
            new EventAggregationDefinition { GroupBy = new[] { "Who" }, MaximumGroups = 1 });

        EventReport report = EventAggregationReportFactory.Create(bounded);
        EventReportRow metadata = Assert.Single(report.Rows);

        Assert.Equal("ResultMetadata", metadata.Values["ResultKind"]);
        Assert.Equal(false, metadata.Values["AggregationComplete"]);
        Assert.Equal(bounded.Diagnostic, metadata.Values["Diagnostic"]);
        Assert.Equal(bounded.InputRows, metadata.Values["InputRows"]);
        Assert.Equal(bounded.Diagnostic, report.CompletenessDiagnostic);
    }

    [Fact]
    public void OccurrenceBoundFailureEmitsAReportMetadataRow() {
        EventReport source = EventReportEngine.CreateStored(
            new[] {
                CreateRow(1, new Dictionary<string, object?>()),
                CreateRow(2, new Dictionary<string, object?>())
            },
            new[] {
                new EventReportSectionSchema {
                    Name = "SyntheticSecurityEvent",
                    DisplayName = "Synthetic security events",
                    Kind = EventReportSectionKind.Custom,
                    Columns = Array.Empty<EventReportColumnSchema>()
                }
            });
        EventOccurrenceResult bounded = EventOccurrenceEngine.Group(
            source.Rows,
            new EventOccurrenceOptions {
                Mode = EventDuplicateMode.Transport,
                MaximumObservations = 1
            });

        EventReport report = EventOccurrenceReportFactory.Create(bounded, source);
        EventReportRow metadata = Assert.Single(report.Rows);

        Assert.True(report.ScanLimitReached);
        Assert.Equal("ResultMetadata", metadata.Values["ResultKind"]);
        Assert.Equal(false, metadata.Values["IsComplete"]);
        Assert.Contains("MaximumObservations", Assert.IsType<string>(metadata.Values["Diagnostic"]));
    }

    [Fact]
    public void OccurrenceObservationBoundConsumesOnlyOneProofRowBeyondTheLimit() {
        int enumerated = 0;
        IEnumerable<EventReportRow> Observations() {
            for (int index = 1; index <= 4; index++) {
                enumerated++;
                if (index == 4) {
                    throw new InvalidOperationException("The occurrence engine read beyond its proof row.");
                }
                yield return CreateRow(index, new Dictionary<string, object?>());
            }
        }

        EventOccurrenceResult result = EventOccurrenceEngine.Group(
            Observations(),
            new EventOccurrenceOptions { MaximumObservations = 2 });

        Assert.False(result.IsComplete);
        Assert.Equal(3, enumerated);
        Assert.Contains("MaximumObservations", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregationStreamsInputBeforeEnforcingStateBounds() {
        int enumerated = 0;
        IEnumerable<EventReportRow> Rows() {
            while (true) {
                enumerated++;
                if (enumerated > 1) {
                    throw new InvalidOperationException("Aggregation enumerated beyond the row that exceeded its state budget.");
                }
                yield return CreateRow(
                    enumerated,
                    new Dictionary<string, object?> { ["Who"] = "Alice" });
            }
        }

        EventAggregationResult result = EventAggregationEngine.Aggregate(
            Rows(),
            new EventAggregationDefinition {
                GroupBy = new[] { "Who" },
                MaximumStateBytes = 1
            });

        Assert.False(result.AggregationComplete);
        Assert.Equal(1, result.InputRows);
        Assert.Equal(1, enumerated);
        Assert.Contains("MaximumStateBytes", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregationStateBudgetIncludesEveryMeasure() {
        EventAggregationMeasure[] measures = Enumerable.Range(1, 10)
            .Select(index => new EventAggregationMeasure {
                Operation = EventAggregationOperation.Count,
                OutputName = $"Count{index}"
            })
            .ToArray();

        EventAggregationResult result = EventAggregationEngine.Aggregate(
            new[] { CreateRow(1, new Dictionary<string, object?>()) },
            new EventAggregationDefinition {
                Measures = measures,
                MaximumStateBytes = 1000
            });

        Assert.False(result.AggregationComplete);
        Assert.Equal(1, result.InputRows);
        Assert.Contains("MaximumStateBytes", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregationAccumulatorConsumesRowsIncrementally() {
        EventAggregationAccumulator accumulator = EventAggregationEngine.CreateAccumulator(
            new EventAggregationDefinition(),
            EventAggregationInputCompleteness.Unknown);

        Assert.True(accumulator.Add(CreateRow(1, new Dictionary<string, object?>())));
        Assert.True(accumulator.Add(CreateRow(2, new Dictionary<string, object?>())));
        EventAggregationResult result = accumulator.Complete();

        Assert.Equal(2, result.InputRows);
        Assert.Equal(2L, Assert.Single(result.Rows).Measures["Count"]);
        Assert.Throws<InvalidOperationException>(() =>
            accumulator.Add(CreateRow(3, new Dictionary<string, object?>())));
    }

    [Fact]
    public void AggregationReportDisambiguatesDomainFieldsFromEnvelopeColumns() {
        EventAggregationResult result = EventAggregationEngine.Aggregate(
            new[] { CreateRow(1, new Dictionary<string, object?> { ["Diagnostic"] = "domain-value" }) },
            new EventAggregationDefinition {
                GroupBy = new[] { "Diagnostic" },
                Measures = new[] {
                    new EventAggregationMeasure {
                        Operation = EventAggregationOperation.Count,
                        OutputName = "InputRows"
                    }
                }
            });

        EventReport report = EventAggregationReportFactory.Create(result);
        EventReportRow row = Assert.Single(report.Rows);

        Assert.Equal("domain-value", row.Values["Group.Diagnostic"]);
        Assert.Equal(1L, row.Values["Measure.InputRows"]);
        Assert.Equal(result.InputRows, row.Values["InputRows"]);
        Assert.Equal(
            report.Sections[0].Columns.Count,
            report.Sections[0].Columns.Select(static column => column.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AggregationRenderersProduceSeparateDeterministicGroupSeries() {
        EventReportRow[] rows = {
            CreateRow(1, new Dictionary<string, object?> { ["Who"] = "Alice" },
                new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc)),
            CreateRow(2, new Dictionary<string, object?> { ["Who"] = "Bob" },
                new DateTime(2026, 8, 22, 10, 1, 0, DateTimeKind.Utc)),
            CreateRow(3, new Dictionary<string, object?> { ["Who"] = "Alice" },
                new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc))
        };
        EventAggregationResult result = EventAggregationEngine.Aggregate(rows, new EventAggregationDefinition {
            GroupBy = new[] { "Who" },
            Bucket = EventAggregationBucket.Day
        });
        string workbook = Path.Combine(Path.GetTempPath(), $"evx-aggregation-{Guid.NewGuid():N}.xlsx");
        try {
            string html = EventAggregationHtmlRenderer.Render(result);
            string saved = EventAggregationExcelRenderer.Save(result, workbook);

            Assert.Contains("Who=Alice", html, StringComparison.Ordinal);
            Assert.Contains("Who=Bob", html, StringComparison.Ordinal);
            Assert.True(new FileInfo(saved).Length > 0);
        } finally {
            if (File.Exists(workbook)) {
                File.Delete(workbook);
            }
        }
    }

    [Fact]
    public void ExcelAggregationRendererDisambiguatesDomainAndMetadataColumns() {
        EventAggregationResult result = EventAggregationEngine.Aggregate(
            new[] {
                CreateRow(1, new Dictionary<string, object?> { ["Bucket"] = "domain-bucket" })
            },
            new EventAggregationDefinition {
                GroupBy = new[] { "Bucket" },
                Measures = new[] {
                    new EventAggregationMeasure {
                        Operation = EventAggregationOperation.Count,
                        OutputName = "Bucket Start UTC"
                    }
                }
            });
        string workbook = Path.Combine(Path.GetTempPath(), $"evx-aggregation-collision-{Guid.NewGuid():N}.xlsx");
        try {
            string html = EventAggregationHtmlRenderer.Render(result);
            string saved = EventAggregationExcelRenderer.Save(result, workbook);

            Assert.Contains("domain-bucket", html, StringComparison.Ordinal);
            Assert.True(new FileInfo(saved).Length > 0);
        } finally {
            if (File.Exists(workbook)) {
                File.Delete(workbook);
            }
        }
    }

    private static EventReportRow CreateRow(
        long recordId,
        IReadOnlyDictionary<string, object?> values,
        DateTime? time = null,
        string source = "dc1.ad.evotec.xyz",
        string collector = "dc1.ad.evotec.xyz",
        string container = "Security") {

        var row = new EventReportRow {
            TimeCreated = time ?? new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc).AddSeconds(recordId),
            Type = "SyntheticSecurityEvent",
            EventId = 5136,
            RecordId = recordId,
            Provider = "Microsoft-Windows-Security-Auditing",
            SourceLog = "Security",
            ContainerLog = container,
            SourceComputer = source,
            CollectorComputer = collector,
            Values = values
        };
        EventValueNormalizationEngine.Populate(row);
        return row;
    }
}
