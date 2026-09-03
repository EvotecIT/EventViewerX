using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventPredicate {
    [Fact]
    public void PredicateRoundTripsAndEvaluatesTypedFields() {
        var source = new EventObject(
            new SyntheticEventRecord(),
            "WEC01",
            EventReadMode.StructuredDataAndMessage);
        var record = new ExampleRecord(source) {
            Who = "EVOTEC\\alice",
            IpAddress = "10.20.30.40",
            Attempts = 4
        };
        EventPredicate predicate = EventPredicate.AllOf(
            EventPredicate.Compare("Who", EventPredicateOperator.In, "EVOTEC\\alice", "EVOTEC\\bob"),
            EventPredicate.Compare("IpAddress", EventPredicateOperator.InSubnet, "10.0.0.0/8"),
            EventPredicate.Compare("Attempts", EventPredicateOperator.GreaterThanOrEqual, 3));

        string json = predicate.ToJson();
        EventPredicate restored = EventPredicate.ParseJson(json);

        Assert.True(EventPredicateEvaluator.Matches(restored, record));
        record.IpAddress = "192.168.1.20";
        Assert.False(EventPredicateEvaluator.Matches(restored, record));
    }

    [Fact]
    public void WildcardsFollowPowerShellCharacterClassAndEscapeSemantics() {
        var values = new Dictionary<string, object?> { ["Who"] = "svc7" };
        EventPredicate digit = EventPredicate.Compare(
            "Who",
            EventPredicateOperator.MatchesWildcard,
            "svc[0-9]");
        EventPredicate letter = EventPredicate.Compare(
            "Who",
            EventPredicateOperator.MatchesWildcard,
            "svc[a-z]");
        EventPredicate literalStar = EventPredicate.Compare(
            "Who",
            EventPredicateOperator.MatchesWildcard,
            "svc`*");

        Assert.True(EventPredicateEvaluator.Matches(digit, values));
        Assert.False(EventPredicateEvaluator.Matches(letter, values));
        values["Who"] = "svc*";
        Assert.True(EventPredicateEvaluator.Matches(literalStar, values));
    }

    [Fact]
    public void PlannerPushesOnlySafeSystemPredicates() {
        EventPredicate predicate = EventPredicate.AllOf(
            EventPredicate.Compare("EventId", EventPredicateOperator.In, 4624, 4625),
            EventPredicate.Compare("TimeCreated", EventPredicateOperator.GreaterThanOrEqual,
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)),
            EventPredicate.Compare("Who", EventPredicateOperator.EndsWith, "alice"));

        EventPredicatePlan plan = EventPredicatePlanner.Plan(predicate);

        Assert.Equal(new[] { 4624, 4625 }, plan.NativeFilter!.EventIds);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), plan.NativeFilter.StartTime);
        Assert.NotNull(plan.ManagedPredicate);
        Assert.Equal(EventPredicateKind.All, plan.ManagedPredicate!.Kind);
        Assert.Equal(2, plan.Steps.Count(static step => step.Stage == EventPredicatePlanStage.Native));
        Assert.Equal(2, plan.Steps.Count(static step => step.Stage == EventPredicatePlanStage.Managed));
        Assert.Contains(plan.Steps, static step => step.Expression == "Exact predicate verification");
    }

    [Fact]
    public void IgnoreCaseProviderPredicatesRemainManagedUntilExactVerification() {
        EventPredicate ignoreCase = EventPredicate.Compare(
            "ProviderName",
            EventPredicateOperator.Equal,
            "microsoft-windows-security-auditing");
        EventPredicate exactCase = EventPredicate.Compare(
            "ProviderName",
            EventPredicateOperator.Equal,
            "Microsoft-Windows-Security-Auditing");
        exactCase.IgnoreCase = false;

        EventPredicatePlan ignoreCasePlan = EventPredicatePlanner.Plan(ignoreCase);
        EventPredicatePlan exactCasePlan = EventPredicatePlanner.Plan(exactCase);

        Assert.Null(ignoreCasePlan.NativeFilter);
        Assert.NotNull(ignoreCasePlan.ManagedPredicate);
        Assert.Equal(
            new[] { "Microsoft-Windows-Security-Auditing" },
            exactCasePlan.NativeFilter!.ProviderNames);
        Assert.NotNull(exactCasePlan.ManagedPredicate);
    }

    [Fact]
    public void DateTimeComparisonsNormalizeLocalAndUtcValuesToOneTimeline() {
        DateTime local = DateTime.SpecifyKind(
            DateTime.Today.AddHours(12),
            DateTimeKind.Local);
        DateTime utc = local.ToUniversalTime();
        var fields = new Dictionary<string, object?> { ["TimeCreated"] = local };

        Assert.True(EventPredicateEvaluator.Matches(
            EventPredicate.Compare("TimeCreated", EventPredicateOperator.Equal, utc),
            fields));
        Assert.True(EventPredicateEvaluator.Matches(
            EventPredicate.Compare("TimeCreated", EventPredicateOperator.GreaterThan, utc.AddMinutes(-1)),
            fields));
        Assert.True(EventPredicateEvaluator.Matches(
            EventPredicate.Compare("TimeCreated", EventPredicateOperator.LessThan, utc.AddMinutes(1)),
            fields));
    }

    [Fact]
    public void NativeFilterIntersectionsNormalizeMixedDateTimeKindsToUtc() {
        DateTime local = DateTime.SpecifyKind(
            new DateTime(2026, 1, 15, 10, 0, 0),
            DateTimeKind.Local);
        DateTime laterUtc = local.ToUniversalTime().AddMinutes(30);
        DateTime earlierUtc = local.ToUniversalTime().AddMinutes(-30);

        Assert.True(EventFilterIntersection.TryCreate(
            new EventFilter { StartTime = local },
            new EventFilter { StartTime = laterUtc },
            out EventFilter lowerBound));
        Assert.True(EventFilterIntersection.TryCreate(
            new EventFilter { EndTime = local },
            new EventFilter { EndTime = earlierUtc },
            out EventFilter upperBound));

        Assert.Equal(laterUtc, lowerBound.StartTime);
        Assert.Equal(DateTimeKind.Utc, lowerBound.StartTime!.Value.Kind);
        Assert.Equal(earlierUtc, upperBound.EndTime);
        Assert.Equal(DateTimeKind.Utc, upperBound.EndTime!.Value.Kind);
    }

    [Fact]
    public void NativeProviderIntersectionsPreserveExactCase() {
        Assert.False(EventFilterIntersection.TryCreate(
            new EventFilter { ProviderNames = new[] { "ADSync" } },
            new EventFilter { ProviderNames = new[] { "adsync" } },
            out _));
        Assert.True(EventFilterIntersection.TryCreate(
            new EventFilter { ProviderNames = new[] { "ADSync" } },
            new EventFilter { ProviderNames = new[] { "ADSync" } },
            out EventFilter exact));

        Assert.Equal(new[] { "ADSync" }, exact.ProviderNames);
    }

    [Fact]
    public void PredicatePlannerNormalizesMixedDateTimeKindsBeforeIntersectingNativeBounds() {
        DateTime local = DateTime.SpecifyKind(
            new DateTime(2026, 1, 15, 10, 0, 0),
            DateTimeKind.Local);
        DateTime laterUtc = local.ToUniversalTime().AddMinutes(30);
        EventPredicate predicate = EventPredicate.AllOf(
            EventPredicate.Compare("TimeCreated", EventPredicateOperator.GreaterThanOrEqual, local),
            EventPredicate.Compare("TimeCreated", EventPredicateOperator.GreaterThanOrEqual, laterUtc));

        EventPredicatePlan plan = EventPredicatePlanner.Plan(predicate);

        Assert.Equal(laterUtc, plan.NativeFilter!.StartTime);
        Assert.Equal(DateTimeKind.Utc, plan.NativeFilter.StartTime!.Value.Kind);
    }

    [Fact]
    public void CollectionNotEqualMatchesWhenAnyItemDiffers() {
        EventPredicate predicate = EventPredicate.Compare(
            "Privileges",
            EventPredicateOperator.NotEqual,
            "SeDebugPrivilege");

        Assert.True(EventPredicateEvaluator.Matches(
            predicate,
            new Dictionary<string, object?> {
                ["Privileges"] = new[] { "SeDebugPrivilege", "SeBackupPrivilege" }
            }));
        Assert.False(EventPredicateEvaluator.Matches(
            predicate,
            new Dictionary<string, object?> {
                ["Privileges"] = new[] { "SeDebugPrivilege", "SeDebugPrivilege" }
            }));
        Assert.False(EventPredicateEvaluator.Matches(
            predicate,
            new Dictionary<string, object?> { ["Privileges"] = Array.Empty<string>() }));
    }

    [Fact]
    public void TypedDomainWhenRemainsManagedWhileSourceTimestampStaysNative() {
        EventPredicateBuilder builder = EventPredicateBuilder.ForType(EventType.OSCrash);
        DateTime boundary = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        EventPredicate domainWhen = builder.Normalize(
            EventPredicate.Compare("When", EventPredicateOperator.GreaterThanOrEqual, boundary));
        EventPredicate sourceTime = builder.Normalize(
            EventPredicate.Compare("TimeCreated", EventPredicateOperator.GreaterThanOrEqual, boundary));

        EventPredicatePlan domainPlan = EventPredicatePlanner.Plan(domainWhen);
        EventPredicatePlan sourcePlan = EventPredicatePlanner.Plan(sourceTime);

        Assert.Equal(EventFieldFilterStage.Managed, builder.Field("When").FilterStage);
        Assert.Null(domainPlan.NativeFilter);
        Assert.NotNull(domainPlan.ManagedPredicate);
        Assert.Equal(boundary, sourcePlan.NativeFilter!.StartTime);
    }

    [Fact]
    public void ContradictoryNativePredicatesRemainExactAndMatchNothing() {
        EventPredicate predicate = EventPredicate.AllOf(
            EventPredicate.Compare("EventId", EventPredicateOperator.Equal, 1),
            EventPredicate.Compare("EventId", EventPredicateOperator.Equal, 2));

        EventPredicatePlan plan = EventPredicatePlanner.Plan(predicate);

        Assert.Null(plan.NativeFilter);
        Assert.NotNull(plan.ManagedPredicate);
        Assert.DoesNotContain(plan.Steps, static step => step.Stage == EventPredicatePlanStage.Native);
        Assert.Contains(plan.Steps, static step => step.Expression == "Contradictory native predicates");
        Assert.False(EventPredicateEvaluator.Matches(
            plan.ManagedPredicate!,
            new Dictionary<string, object?> { ["EventId"] = 1 }));
    }

    [Fact]
    public void BuilderExposesDefinitionFieldsAndValidatesNames() {
        EventPredicateBuilder builder = EventPredicateBuilder.ForType(EventType.ADUserLogonFailed);

        Assert.Contains(builder.Fields, static field => field.Name == "Who");
        Assert.All(builder.Fields, static field => Assert.False(string.IsNullOrWhiteSpace(field.Description)));
        EventPredicateField who = builder.Field("Who");
        Assert.Equal(typeof(string), who.ValueType);
        Assert.Equal(EventFieldFilterStage.Managed, who.FilterStage);
        Assert.Contains(EventPredicateOperator.MatchesWildcard, who.SupportedOperators);
        Assert.False(who.IsCommon);
        EventPredicateField level = builder.Field("Level");
        Assert.Equal(typeof(Level?), level.ValueType);
        Assert.Equal(EventFieldFilterStage.Native, level.FilterStage);
        EventPredicate predicate = builder.AllOf(
            who.StartsWith("EVOTEC\\"),
            builder.Field("IpAddress").MatchesSubnet("10.0.0.0/8"));

        Assert.Equal(EventPredicateKind.All, predicate.Kind);
        Assert.Throws<ArgumentException>(() => builder.Field("DefinitelyMissing"));
    }

    [Fact]
    public void LevelNamesRemainTypedAndPushToNativeNumericValues() {
        EventPredicateBuilder builder = EventPredicateBuilder.ForType(EventType.ADUserLogonFailed);
        EventPredicate predicate = builder.Normalize(builder.Field("Level").Equal(Level.LogAlways));
        EventPredicatePlan plan = EventPredicatePlanner.Plan(predicate);
        var source = new EventObject(
            new SyntheticEventRecord(),
            "WEC01",
            EventReadMode.StructuredDataAndMessage);
        var record = new ExampleRecord(source);

        Assert.Equal(new byte[] { 0 }, plan.NativeFilter!.Levels);
        Assert.True(EventPredicateEvaluator.Matches(predicate, record));
        Assert.True(EventPredicateEvaluator.Matches(
            predicate,
            new Dictionary<string, object?> { ["Level"] = Level.LogAlways }));
    }

    [Fact]
    public void InvalidLevelLiteralFallsBackWithoutPlannerFailure() {
        EventPredicate predicate = EventPredicate.Compare(
            "Level",
            EventPredicateOperator.Equal,
            "NotAWindowsLevel");

        EventPredicatePlan plan = EventPredicatePlanner.Plan(predicate);

        Assert.False(plan.HasNativeFilter);
        Assert.NotNull(plan.ManagedPredicate);
    }

    [Fact]
    public void BuilderRejectsLiteralsThatCannotConvertToTheProjectedFieldType() {
        EventPredicateBuilder builder = EventPredicateBuilder.ForType(EventType.ADUserLogonFailed);

        ArgumentException invalidNumber = Assert.Throws<ArgumentException>(() => builder.Normalize(
            EventPredicate.Compare("EventId", EventPredicateOperator.Equal, "not-a-number")));
        ArgumentException overflow = Assert.Throws<ArgumentException>(() => builder.Normalize(
            EventPredicate.Compare("EventId", EventPredicateOperator.Equal, "999999999999999999999")));

        Assert.Contains("EventId", invalidNumber.Message, StringComparison.Ordinal);
        Assert.Contains("EventId", overflow.Message, StringComparison.Ordinal);
        Assert.False(EventPredicateEvaluator.Matches(
            EventPredicate.Compare("EventId", EventPredicateOperator.NotEqual, "not-a-number"),
            new Dictionary<string, object?> { ["EventId"] = 4625 }));
        Assert.False(EventPredicateEvaluator.Matches(
            EventPredicate.Not(EventPredicate.Compare(
                "EventId",
                EventPredicateOperator.Equal,
                "not-a-number")),
            new Dictionary<string, object?> { ["EventId"] = 4625 }));
    }

    [Fact]
    public void RegexTimeoutIsAConservativeNonMatch() {
        EventPredicate predicate = EventPredicate.Compare(
            "Who",
            EventPredicateOperator.MatchesRegex,
            "^(a+)+$");
        string value = new string('a', 100_000) + "!";

        Assert.False(EventPredicateEvaluator.Matches(
            predicate,
            new Dictionary<string, object?> { ["Who"] = value }));
    }

    [Fact]
    public void BuiltInIpAddressFieldsExposeSubnetMatchingAcrossTypedDefinitions() {
        (EventType Type, string Field)[] cases = {
            (EventType.ADUserLogonFailed, "IpAddress"),
            (EventType.DhcpLeaseCreated, "IPAddress"),
            (EventType.NetworkAccessAuthenticationPolicy, "NASIPv4Address"),
            (EventType.ADLdapBindingDetails, "RemoteIp"),
            (EventType.ADSMBServerAuditV1, "ClientAddress")
        };

        foreach ((EventType type, string fieldName) in cases) {
            EventPredicateField field = EventPredicateBuilder.ForType(type).Field(fieldName);
            Assert.Contains(EventPredicateOperator.InSubnet, field.SupportedOperators);
            Assert.Equal(EventPredicateOperator.InSubnet, field.MatchesSubnet("10.0.0.0/8").Operator);
        }
    }

    [Fact]
    public void PredicateRejectsInvalidShapesAndBounds() {
        Assert.Throws<InvalidDataException>(() => EventPredicate.Compare(
            "Who",
            EventPredicateOperator.Equal,
            "alice",
            "bob"));
        Assert.Throws<InvalidDataException>(() => new EventPredicate {
            Kind = EventPredicateKind.Not,
            Children = Array.Empty<EventPredicate>()
        }.Validate());
        Assert.Throws<InvalidDataException>(() => EventPredicate.Compare(
            "Who",
            EventPredicateOperator.MatchesRegex,
            "[unterminated"));
        Assert.Throws<InvalidDataException>(() => EventPredicate.Compare(
            "IpAddress",
            EventPredicateOperator.InSubnet,
            "10.0.0.0/99"));
        Assert.Throws<InvalidDataException>(() => EventPredicate.Compare(
            "Who",
            EventPredicateOperator.In,
            Enumerable.Range(0, 1025).Cast<object?>().ToArray()));
    }

    [Fact]
    public void PredicatePreservesCaseSensitivityAndMissingValuesDoNotSort() {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            ["Who"] = "EVOTEC\\Alice"
        };
        EventPredicate insensitive = EventPredicate.Compare(
            "Who",
            EventPredicateOperator.Equal,
            "evotec\\alice");
        EventPredicate sensitive = EventPredicate.Compare(
            "Who",
            EventPredicateOperator.Equal,
            "evotec\\alice");
        sensitive.IgnoreCase = false;

        Assert.True(EventPredicateEvaluator.Matches(insensitive, fields));
        Assert.False(EventPredicateEvaluator.Matches(sensitive, fields));
        Assert.False(EventPredicateEvaluator.Matches(
            EventPredicate.Compare("Missing", EventPredicateOperator.LessThan, 1),
            fields));
        Assert.False(EventPredicateEvaluator.Matches(
            EventPredicate.Compare("Missing", EventPredicateOperator.GreaterThan, 1),
            fields));
        Assert.False(EventPredicate.ParseJson(sensitive.ToJson()).IgnoreCase);
    }

    [Fact]
    public void StronglyTypedExpressionUsesTheSharedSerializableModel() {
        string[] users = { "EVOTEC\\alice", "EVOTEC\\bob" };

        EventPredicate predicate = EventPredicate.FromExpression<ExampleRecord>(record =>
            users.Contains(record.Who) && record.Attempts >= 3 && record.IpAddress.StartsWith("10."));

        Assert.Equal(EventPredicateKind.All, predicate.Kind);
        Assert.Contains("Who", predicate.ToJson(), StringComparison.Ordinal);
        Assert.Contains(
            Flatten(predicate),
            static item => item.Field == "Attempts" && item.Operator == EventPredicateOperator.GreaterThanOrEqual);
    }

    [Fact]
    public void PredicatesRejectNullArgumentsForValueBasedOperators() {
        string value = null!;

        InvalidDataException contains = Assert.Throws<InvalidDataException>(() =>
            EventPredicate.FromExpression<ExampleRecord>(record => record.Who.Contains(value)));
        InvalidDataException startsWith = Assert.Throws<InvalidDataException>(() =>
            EventPredicate.FromExpression<ExampleRecord>(record => record.Who.StartsWith(value)));
        InvalidDataException endsWith = Assert.Throws<InvalidDataException>(() =>
            EventPredicate.FromExpression<ExampleRecord>(record => record.Who.EndsWith(value)));
        InvalidDataException ordered = Assert.Throws<InvalidDataException>(() => new EventPredicate {
            Kind = EventPredicateKind.Comparison,
            Field = "Attempts",
            Operator = EventPredicateOperator.GreaterThan,
            Values = new string?[] { null }
        }.Validate());

        Assert.All(
            new[] { contains, startsWith, endsWith, ordered },
            static exception => Assert.Contains("cannot be null", exception.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void StronglyTypedExpressionsPreserveCSharpStringCaseSensitivity() {
        string[] users = { "ADMIN", "SERVICE" };
        EventPredicate equality = EventPredicate.FromExpression<ExampleRecord>(record => record.Who == "ADMIN");
        EventPredicate membership = EventPredicate.FromExpression<ExampleRecord>(record => users.Contains(record.Who));
        EventPredicate prefix = EventPredicate.FromExpression<ExampleRecord>(record => record.IpAddress.StartsWith("ABC"));
        var record = new ExampleRecord(new EventObject(
            new SyntheticEventRecord(),
            Environment.MachineName,
            EventReadMode.StructuredDataAndMessage)) {
            Who = "admin",
            IpAddress = "abc-value"
        };
        var exactMember = new ExampleRecord(new EventObject(
            new SyntheticEventRecord(),
            Environment.MachineName,
            EventReadMode.StructuredDataAndMessage)) {
            Who = "ADMIN"
        };

        Assert.All(
            new[] { equality, membership, prefix }.SelectMany(Flatten)
                .Where(static predicate => predicate.Kind == EventPredicateKind.Comparison),
            static predicate => Assert.False(predicate.IgnoreCase));
        Assert.Equal(users, membership.Values);
        Assert.True(EventPredicateEvaluator.Matches(membership, exactMember));
        Assert.False(EventPredicateEvaluator.Matches(equality, record));
        Assert.False(EventPredicateEvaluator.Matches(membership, record));
        Assert.False(EventPredicateEvaluator.Matches(prefix, record));
    }

    [Fact]
    public void StronglyTypedExpressionsPreserveSupportedCapturedCollectionComparers() {
        var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ADMIN" };
        EventPredicate predicate = EventPredicate.FromExpression<ExampleRecord>(record =>
            users.Contains(record.Who));
        string[] explicitUsers = { "ADMIN" };
        EventPredicate explicitPredicate = EventPredicate.FromExpression<ExampleRecord>(record =>
            explicitUsers.Contains(record.Who, StringComparer.OrdinalIgnoreCase));
        var record = new ExampleRecord(new EventObject(
            new SyntheticEventRecord(),
            Environment.MachineName,
            EventReadMode.StructuredDataAndMessage)) {
            Who = "admin"
        };

        Assert.True(predicate.IgnoreCase);
        Assert.True(explicitPredicate.IgnoreCase);
        Assert.True(EventPredicateEvaluator.Matches(predicate, record));
        Assert.True(EventPredicateEvaluator.Matches(explicitPredicate, record));
    }

    [Fact]
    public void StronglyTypedExpressionsRejectUnsupportedCapturedCollectionComparers() {
        var users = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase) { "ADMIN" };

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            EventPredicate.FromExpression<ExampleRecord>(record => users.Contains(record.Who)));

        Assert.Contains("cannot be represented", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StronglyTypedExpressionsRejectOuterCollectionMembershipForCollectionFields() {
        var allowed = new List<List<string>> {
            new() { "SeDebugPrivilege" }
        };

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            EventPredicate.FromExpression<Rules.ActiveDirectory.ADUserPrivilegeUse>(record =>
                allowed.Contains(record.Privileges)));

        Assert.Contains("collection field", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reference or comparer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StronglyTypedExpressionsPreserveReferenceNullChecksForCollections() {
        EventPredicate isNull = EventPredicate.FromExpression<Rules.ActiveDirectory.ADUserPrivilegeUse>(
            record => record.Privileges == null);
        EventPredicate isNotNull = EventPredicate.FromExpression<Rules.ActiveDirectory.ADUserPrivilegeUse>(
            record => record.Privileges != null);
        var record = new Rules.ActiveDirectory.ADUserPrivilegeUse(new EventObject(
            new SyntheticEventRecord(),
            Environment.MachineName,
            EventReadMode.StructuredDataAndMessage));

        Assert.Equal(EventPredicateOperator.IsNull, isNull.Operator);
        Assert.Equal(EventPredicateOperator.IsNotNull, isNotNull.Operator);
        Assert.Empty(isNull.Values);
        Assert.Empty(isNotNull.Values);
        Assert.False(EventPredicateEvaluator.Matches(isNull, record));
        Assert.True(EventPredicateEvaluator.Matches(isNotNull, record));
    }

    [Fact]
    public void StronglyTypedExpressionsRejectNonNullCollectionReferenceComparisons() {
        var privileges = new List<string> { "SeDebugPrivilege" };

        NotSupportedException equal = Assert.Throws<NotSupportedException>(() =>
            EventPredicate.FromExpression<Rules.ActiveDirectory.ADUserPrivilegeUse>(record =>
                record.Privileges == privileges));
        NotSupportedException notEqual = Assert.Throws<NotSupportedException>(() =>
            EventPredicate.FromExpression<Rules.ActiveDirectory.ADUserPrivilegeUse>(record =>
                record.Privileges != privileges));

        Assert.Contains("reference comparison", equal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reference comparison", notEqual.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomMetadataPredicateUsesNativePrefilterAndExactProjectionCheck() {
        var definition = new EventDefinition {
            Name = "ServiceChange",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "System",
                    EventIds = new[] { 7040 },
                    ProviderNames = new[] { "Service Control Manager" }
                }
            },
            Fields = new[] {
                new EventDefinitionField {
                    Name = "ProjectedId",
                    Source = EventFieldSource.Metadata,
                    SourceName = nameof(EventObject.Id),
                    ValueKind = EventFieldValueKind.Int32
                },
                new EventDefinitionField {
                    Name = "ServiceName",
                    Source = EventFieldSource.Data,
                    SourceName = "param4"
                }
            }
        };
        EventPredicateBuilder builder = EventPredicateBuilder.ForDefinition(definition);
        Assert.Equal(EventFieldFilterStage.Native, builder.Field("ProjectedId").Definition.FilterStage);
        Assert.Contains(builder.Fields, static field => field.Name == "EventId" && field.Definition.IsCommon);
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        var query = new EventDefinitionQuery(definition) {
            Paths = new[] { fixture },
            Predicate = builder.AllOf(
                builder.Field("ProjectedId").Equal(7040),
                builder.Field("ServiceName").Equal("BITS")),
            Oldest = true
        };
        var info = new EventDefinitionQueryExecutionInfo();

        List<CustomEventRecord> records = await ReadAllAsync(
            EventDefinitionEngine.ReadAsync(query, info));

        Assert.NotEmpty(records);
        Assert.All(records, static record => Assert.Equal(7040, record.Values["ProjectedId"]));
        Assert.All(records, static record => Assert.Equal("BITS", record.Values["ServiceName"]));
        Assert.Equal(new[] { 7040 }, info.PredicatePlan!.NativeFilter!.EventIds);
        Assert.False(info.PredicatePlan.IsFullyNative);
        Assert.Contains(info.PredicatePlan.Steps,
            static step => step.Stage == EventPredicatePlanStage.Native);
        Assert.Contains(info.PredicatePlan.Steps,
            static step => step.Stage == EventPredicatePlanStage.Managed);
    }

    [Fact]
    public void ConvertedCustomMetadataStaysManagedWhenNativeComparisonSemanticsDiffer() {
        var definition = new EventDefinition {
            Name = "GuidProvider",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "Application",
                    EventIds = new[] { 1 },
                    ProviderNames = new[] { "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" }
                }
            },
            Fields = new[] {
                new EventDefinitionField {
                    Name = "ProviderId",
                    Source = EventFieldSource.Metadata,
                    SourceName = nameof(EventObject.ProviderName),
                    ValueKind = EventFieldValueKind.Guid
                }
            }
        };
        EventPredicateBuilder builder = EventPredicateBuilder.ForDefinition(definition);
        EventPredicate predicate = builder.Field("ProviderId")
            .Equal(Guid.Parse("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE"));

        EventPredicatePlan plan = EventDefinitionEngine.PlanPredicate(definition, predicate);

        Assert.Equal(EventFieldFilterStage.Managed, builder.Field("ProviderId").FilterStage);
        Assert.Null(plan.NativeFilter);
        Assert.Equal("ProviderId", plan.ManagedPredicate!.Field);
        Assert.Contains(plan.Steps, static step => step.Stage == EventPredicatePlanStage.Managed);
    }

    [Fact]
    public async Task CustomFieldNamedLikeNativeMetadataStaysManaged() {
        var definition = new EventDefinition {
            Name = "ServiceChangeWithProviderLabel",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "System",
                    EventIds = new[] { 7040 },
                    ProviderNames = new[] { "Service Control Manager" }
                }
            },
            Fields = new[] {
                new EventDefinitionField {
                    Name = "ProviderName",
                    Source = EventFieldSource.Data,
                    SourceName = "param4"
                }
            }
        };
        EventPredicateBuilder builder = EventPredicateBuilder.ForDefinition(definition);
        Assert.Equal(EventFieldFilterStage.Managed, builder.Field("ProviderName").FilterStage);
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        var query = new EventDefinitionQuery(definition) {
            Paths = new[] { fixture },
            Predicate = builder.Field("ProviderName").Equal("BITS"),
            Oldest = true
        };
        var info = new EventDefinitionQueryExecutionInfo();

        List<CustomEventRecord> records = await ReadAllAsync(
            EventDefinitionEngine.ReadAsync(query, info));

        Assert.NotEmpty(records);
        Assert.All(records, static record => Assert.Equal("BITS", record.Values["ProviderName"]));
        Assert.Null(info.PredicatePlan!.NativeFilter);
        Assert.Contains(info.PredicatePlan.Steps,
            static step => step.Stage == EventPredicatePlanStage.Managed);
    }

    [Fact]
    public async Task CustomAliasClaimingNativeNameResolvesToCustomValue() {
        var definition = new EventDefinition {
            Name = "ServiceChangeWithProviderAlias",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "System",
                    EventIds = new[] { 7040 },
                    ProviderNames = new[] { "Service Control Manager" }
                }
            },
            Fields = new[] {
                new EventDefinitionField {
                    Name = "ServiceLabel",
                    Aliases = new[] { "ProviderName" },
                    Source = EventFieldSource.Data,
                    SourceName = "param4"
                }
            }
        };
        EventPredicateBuilder builder = EventPredicateBuilder.ForDefinition(definition);
        Assert.Equal("ServiceLabel", builder.Field("ProviderName").Name);
        Assert.DoesNotContain(builder.Fields, static field => field.Name == "ProviderName");
        EventPredicatePlan explained = EventDefinitionEngine.PlanPredicate(
            definition,
            EventPredicate.Compare("ProviderName", EventPredicateOperator.Equal, "BITS"));
        Assert.Null(explained.NativeFilter);
        Assert.Equal("ServiceLabel", explained.ManagedPredicate!.Field);
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        var query = new EventDefinitionQuery(definition) {
            Paths = new[] { fixture },
            Predicate = EventPredicate.Compare("ProviderName", EventPredicateOperator.Equal, "BITS"),
            Oldest = true
        };
        var info = new EventDefinitionQueryExecutionInfo();

        List<CustomEventRecord> records = await ReadAllAsync(
            EventDefinitionEngine.ReadAsync(query, info));

        Assert.NotEmpty(records);
        Assert.All(records, static record => Assert.Equal("BITS", record.Values["ServiceLabel"]));
        Assert.Null(info.PredicatePlan!.NativeFilter);
        Assert.Equal("ServiceLabel", info.PredicatePlan.ManagedPredicate!.Field);
    }

    private sealed class ExampleRecord : EventTypeRecord {
        internal ExampleRecord(EventObject source) : base(source) {
        }

        public string Who { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Attempts { get; set; }
    }

    private sealed class SyntheticEventRecord : EventRecord {
        public override string ProviderName => "Microsoft-Windows-Security-Auditing";
        public override string LogName => "Security";
        public override string MachineName => "ad0.ad.evotec.xyz";
        public override int Id => 4625;
        public override byte? Level => 0;
        public override int? Task => 12544;
        public override long? Keywords => 0;
        public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();
        public override short? Opcode => 0;
        public override string OpcodeDisplayName => string.Empty;
        public override string TaskDisplayName => string.Empty;
        public override Guid? ProviderId => null;
        public override Guid? ActivityId => null;
        public override Guid? RelatedActivityId => null;
        public override int? ProcessId => 1;
        public override int? ThreadId => 1;
        public override string LevelDisplayName => "Information";
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc);
        public override int? Qualifiers => null;
        public override long? RecordId => 42;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;
        public override string FormatDescription() => "A synthetic event.";
        public override string FormatDescription(IEnumerable<object> values) => FormatDescription();
        public override string ToXml() => "<Event><EventData /></Event>";
    }

    private static IEnumerable<EventPredicate> Flatten(EventPredicate predicate) {
        yield return predicate;
        foreach (EventPredicate child in predicate.Children) {
            foreach (EventPredicate nested in Flatten(child)) {
                yield return nested;
            }
        }
    }

    private static async Task<List<T>> ReadAllAsync<T>(IAsyncEnumerable<T> source) {
        var values = new List<T>();
        await foreach (T value in source) {
            values.Add(value);
        }
        return values;
    }
}
