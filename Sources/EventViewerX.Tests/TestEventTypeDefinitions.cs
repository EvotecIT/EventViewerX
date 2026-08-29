using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventTypeDefinitions {
    [Fact]
    public void CatalogDescribesEveryEnumValueAndEveryLeafHasARecordType() {
        EventTypeDefinition[] definitions = EventTypeCatalog
            .GetDefinitions()
            .ToArray();

        Assert.Equal(Enum.GetValues(typeof(EventType)).Length, definitions.Length);
        Assert.Equal(definitions.Length, definitions.Select(static item => item.Type).Distinct().Count());
        Assert.All(
            definitions.Where(static item => !item.IsComposite),
            static definition => {
                Assert.NotNull(definition.RecordType);
                Assert.NotEmpty(definition.Sources);
                Assert.NotEmpty(definition.Fields);
                Assert.Contains(definition.Fields, static field =>
                    !field.IsCommon && field.Name is not "EventIds" and not "LogName" and not "Type");
            });
    }

    [Fact]
    public void CompositeDefinitionsExpandToDistinctLeafTypesAndNativeSources() {
        IReadOnlyList<EventType> expanded = EventTypeCatalog.Expand(
            new[] {
                EventType.ActiveDirectoryAuthentication,
                EventType.KerberosActivity
            });

        Assert.Contains(EventType.ADUserLogonFailed, expanded);
        Assert.Contains(EventType.KerberosTGTRequest, expanded);
        Assert.Equal(expanded.Count, expanded.Distinct().Count());
        Assert.DoesNotContain(EventType.ActiveDirectoryAuthentication, expanded);
        Assert.DoesNotContain(EventType.KerberosActivity, expanded);

        IReadOnlyList<EventSourceDefinition> sources = EventTypeCatalog.GetSources(
            new[] { EventType.ActiveDirectoryAuthentication });
        EventSourceDefinition security = Assert.Single(
            sources,
            static source => source.LogName == "Security");
        Assert.Contains(4625, security.EventIds);
        Assert.Contains(4768, security.EventIds);
    }

    [Fact]
    public void CompositeDefinitionsExposeTheExpandedFilterFieldUnion() {
        EventTypeDefinition definition = EventTypeCatalog.GetDefinition(
            EventType.ActiveDirectoryAuthentication);
        EventPredicateBuilder builder = EventPredicateBuilder.ForType(
            EventType.ActiveDirectoryAuthentication);

        Assert.True(definition.IsComposite);
        Assert.Contains(definition.Fields, static field => field.Name == "Who");
        Assert.Contains(definition.Fields, static field => field.Name == "IpAddress");
        Assert.Equal(
            builder.Fields.Select(static field => field.Name),
            definition.Fields.Where(static field => field.IsFilterable).Select(static field => field.Name));
    }

    [Fact]
    public void ForwardedEventUsesOriginalChannelForTypedRoutingAndPreservesContainerIdentity() {
        var source = new ForwardedSecurityEventRecord();
        var snapshot = new EventObject(source, "WEC01", EventReadMode.Metadata) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };

        EventTypeRecord? typed = EventTypeCatalog.CreateEventRule(
            snapshot,
            new[] { EventType.ActiveDirectoryAuthentication });

        Assert.NotNull(typed);
        Assert.IsType<Rules.ActiveDirectory.ADUserLogonFailed>(typed);
        Assert.Equal("Security", typed.SourceLogName);
        Assert.Equal("ForwardedEvents", typed.ContainerLogName);
        Assert.Equal("source-dc.ad.evotec.xyz", typed.SourceComputer);
        Assert.Equal("WEC01", typed.CollectorComputer);
    }

    [Fact]
    public void CollectorFilterCombinesOriginalChannelWithTypedNativeFilter() {
        string native = EventFilterCompiler.BuildXPath(
            new EventFilter {
                EventIds = new[] { 4624, 4625 },
                StartTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

        string collector = EventTypeEngine.AddOriginalChannelPredicate(
            native,
            "Security");

        Assert.StartsWith("(*[System[", collector, StringComparison.Ordinal);
        Assert.Contains("Channel='Security'", collector, StringComparison.Ordinal);
        Assert.Contains("EventID=4624", collector, StringComparison.Ordinal);
        Assert.Contains("EventID=4625", collector, StringComparison.Ordinal);
        Assert.Contains("TimeCreated", collector, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduledTaskUpdatedPrefersCurrentTaskXmlAndRetainsLegacyFallback() {
        var currentSnapshot = new EventObject(
            new ForwardedSecurityEventRecord(4702),
            "DC01",
            EventReadMode.Metadata);
        currentSnapshot.Data["TaskContent"] = "<Task version=\"legacy\" />";
        currentSnapshot.Data["TaskContentNew"] = "<Task version=\"current\" />";
        var legacySnapshot = new EventObject(
            new ForwardedSecurityEventRecord(4702),
            "DC01",
            EventReadMode.Metadata);
        legacySnapshot.Data["TaskContent"] = "<Task version=\"legacy\" />";

        var current = Assert.IsType<Rules.Windows.ScheduledTaskUpdated>(
            EventTypeCatalog.CreateEventRule(currentSnapshot, new[] { EventType.ScheduledTaskUpdated }));
        var legacy = Assert.IsType<Rules.Windows.ScheduledTaskUpdated>(
            EventTypeCatalog.CreateEventRule(legacySnapshot, new[] { EventType.ScheduledTaskUpdated }));

        Assert.Equal("<Task version=\"current\" />", current.TaskContent);
        Assert.Equal("<Task version=\"legacy\" />", legacy.TaskContent);
    }

    [Fact]
    public void AuthenticationCompositePrefersNtlmV1ProjectionOverGenericLogon() {
        var snapshot = CreateSecuritySnapshot(4624);
        snapshot.Data["LmPackageName"] = "NTLM V1";

        EventTypeRecord? typed = EventTypeCatalog.CreateEventRule(
            snapshot,
            new[] { EventType.ActiveDirectoryAuthentication });

        Assert.IsType<Rules.ActiveDirectory.ADUserLogonNTLMv1>(typed);
    }

    [Fact]
    public void ProjectionPlanCanBeReusedAcrossEventsWithoutChangingSpecificity() {
        EventTypeProjectionPlan plan = EventTypeCatalog.CompileProjectionPlan(
            new[] { EventType.ActiveDirectoryAuthentication });
        var ntlmSnapshot = CreateSecuritySnapshot(4624);
        ntlmSnapshot.Data["LmPackageName"] = "NTLM V1";
        var genericSnapshot = CreateSecuritySnapshot(4624);

        EventTypeRecord? ntlm = EventTypeCatalog.CreateEventRule(ntlmSnapshot, plan);
        EventTypeRecord? generic = EventTypeCatalog.CreateEventRule(genericSnapshot, plan);

        Assert.Contains(EventType.ADUserLogonNTLMv1, plan.ExpandedTypes);
        Assert.IsType<Rules.ActiveDirectory.ADUserLogonNTLMv1>(ntlm);
        Assert.IsType<Rules.ActiveDirectory.ADUserLogon>(generic);
    }

    [Fact]
    public void EveryCompositeAndSharedNativeCandidateSetCompilesCompletelyAndDeterministically() {
        EventTypeDefinition[] definitions = EventTypeCatalog.GetDefinitions().ToArray();
        EventTypeDefinition[] leaves = definitions.Where(static definition => !definition.IsComposite).ToArray();
        EventTypeProjectionPlan allPlan = EventTypeCatalog.CompileProjectionPlan(
            leaves.Select(static definition => definition.Type));
        EventTypeProjectionPlan repeatedPlan = EventTypeCatalog.CompileProjectionPlan(
            leaves.Select(static definition => definition.Type));
        var sharedSources = leaves
            .SelectMany(definition => definition.Sources.SelectMany(source => source.EventIds.Select(eventId => new {
                definition.Type,
                LogName = source.LogName,
                EventId = eventId
            })))
            .GroupBy(static item => (item.EventId, LogName: item.LogName.ToUpperInvariant()))
            .Where(static group => group.Select(static item => item.Type).Distinct().Count() > 1)
            .ToArray();

        Assert.NotEmpty(sharedSources);
        foreach (var source in sharedSources) {
            EventType[] expected = source.Select(static item => item.Type).Distinct().ToArray();
            EventType[] actual = allPlan.GetCandidates(source.Key.EventId, source.First().LogName)
                .Select(static projector => projector.Type)
                .ToArray();
            EventType[] repeated = repeatedPlan.GetCandidates(source.Key.EventId, source.First().LogName)
                .Select(static projector => projector.Type)
                .ToArray();

            Assert.Equal(expected.Length, actual.Length);
            Assert.Empty(expected.Except(actual));
            Assert.Equal(actual, repeated);
        }

        foreach (EventTypeDefinition composite in definitions.Where(static definition => definition.IsComposite)) {
            EventType[] expanded = EventTypeCatalog.Expand(new[] { composite.Type }).ToArray();
            EventTypeProjectionPlan plan = EventTypeCatalog.CompileProjectionPlan(new[] { composite.Type });
            foreach (EventSourceDefinition source in composite.Sources) {
                foreach (int eventId in source.EventIds) {
                    EventType[] expected = leaves
                        .Where(definition => expanded.Contains(definition.Type) && definition.Sources.Any(candidate =>
                            string.Equals(candidate.LogName, source.LogName, StringComparison.OrdinalIgnoreCase) &&
                            candidate.EventIds.Contains(eventId)))
                        .Select(static definition => definition.Type)
                        .ToArray();
                    EventType[] actual = plan.GetCandidates(eventId, source.LogName)
                        .Select(static projector => projector.Type)
                        .ToArray();

                    Assert.Equal(expected.Length, actual.Length);
                    Assert.Empty(expected.Except(actual));
                }
            }
        }
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(1, false, false)]
    [InlineData(2, true, true)]
    [InlineData(3, false, true)]
    public void ActiveDirectoryChangesRoutesGpoLinksAndPreservesOptionBits(
        int options,
        bool expectedEnabled,
        bool expectedEnforced) {

        var snapshot = CreateSecuritySnapshot(5136);
        snapshot.Data["ObjectClass"] = "organizationalUnit";
        snapshot.Data["AttributeLDAPDisplayName"] = "gPLink";
        snapshot.Data["AttributeValue"] = $"[LDAP://cn={{11111111-2222-3333-4444-555555555555}},cn=policies,cn=system,DC=example,DC=com;{options}]";
        snapshot.Data["OperationType"] = "%%14674";

        var typed = Assert.IsType<Rules.ActiveDirectory.ADGroupPolicyLinks>(
            EventTypeCatalog.CreateEventRule(snapshot, new[] { EventType.ActiveDirectoryChanges }));
        GroupPolicyLinks link = Assert.Single(typed.GroupPolicyLink);

        Assert.Equal(options, link.Options);
        Assert.Equal(expectedEnabled, link.IsEnabled);
        Assert.Equal(expectedEnforced, link.IsEnforced);
    }

    [Theory]
    [InlineData(5136, "versionNumber", typeof(Rules.ActiveDirectory.ADGroupPolicyEdits))]
    [InlineData(5136, "displayName", typeof(Rules.ActiveDirectory.GpoModified))]
    [InlineData(5137, null, typeof(Rules.ActiveDirectory.GpoCreated))]
    [InlineData(5141, null, typeof(Rules.ActiveDirectory.GpoDeleted))]
    public void GroupPolicyCompositePrefersTheMostSpecificLifecycleProjection(
        int eventId,
        string? attributeName,
        Type expectedType) {

        var snapshot = CreateSecuritySnapshot(eventId);
        snapshot.Data["ObjectClass"] = "groupPolicyContainer";
        if (attributeName != null) {
            snapshot.Data["AttributeLDAPDisplayName"] = attributeName;
        }

        EventTypeRecord? typed = EventTypeCatalog.CreateEventRule(
            snapshot,
            new[] { EventType.ActiveDirectoryChanges });

        Assert.IsType(expectedType, typed);
    }

    private static EventObject CreateSecuritySnapshot(int eventId) {
        return new EventObject(
            new ForwardedSecurityEventRecord(eventId),
            "DC01",
            EventReadMode.Metadata);
    }

    private sealed class ForwardedSecurityEventRecord : EventRecord {
        private readonly int _id;

        internal ForwardedSecurityEventRecord(int id = 4625) {
            _id = id;
        }

        public override string ProviderName => "Microsoft-Windows-Security-Auditing";
        public override string LogName => "Security";
        public override string MachineName => "source-dc.ad.evotec.xyz";
        public override int Id => _id;
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
        public override DateTime? TimeCreated => DateTime.UtcNow;
        public override int? Qualifiers => null;
        public override long? RecordId => 42;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;
        public override string FormatDescription() => string.Empty;
        public override string FormatDescription(IEnumerable<object> values) => string.Empty;
        public override string ToXml() => string.Empty;
    }
}
