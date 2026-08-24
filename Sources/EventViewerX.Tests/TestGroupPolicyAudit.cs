using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using EventViewerX;
using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestGroupPolicyAudit {
    [Fact]
    public void DefinitionCoversGpoObjectsScopesAndWmiFilterEvents() {
        EventDefinition definition = GroupPolicyAuditDefinitions.CreateDirectoryChanges();

        EventDefinitionSource source = Assert.Single(definition.Sources);
        Assert.Equal("Security", source.LogName);
        Assert.Equal(new[] { 5136, 5137, 5139, 5141 }, source.EventIds);
        Assert.Equal("Microsoft-Windows-Security-Auditing", Assert.Single(source.ProviderNames));
        Assert.Contains(definition.Fields, static field => field.SourceName == "OldObjectDN");
        Assert.Contains(definition.Fields, static field => field.SourceName == "NewObjectDN");
        Assert.Contains(definition.Fields, static field => field.SourceName == "AttributeLDAPDisplayName");
        Assert.Contains(definition.Fields, static field => field.SourceName == "SubjectUserSid");
        Assert.Contains(definition.Fields, static field => field.SourceName == "OpCorrelationID");
    }

    [Fact]
    public void MovedGpoPreservesOldAndNewIdentity() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        string oldDn = $"CN={{{gpoId}}},OU=Staging,DC=ad,DC=evotec,DC=xyz";
        string newDn = $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz";
        EventObject source = CreateMovedSource(oldDn, newDn);

        GroupPolicyAuditRecord record = GroupPolicyAuditEngine.CreateRecord(source);

        Assert.Equal(GroupPolicyAuditEventKind.Moved, record.Kind);
        Assert.Equal(GroupPolicyAuditTargetKind.GroupPolicyObject, record.TargetKind);
        Assert.Equal(oldDn, record.OldObjectDistinguishedName);
        Assert.Equal(newDn, record.NewObjectDistinguishedName);
        Assert.Equal(newDn, record.ObjectDistinguishedName);
        Assert.Equal(Guid.Parse(gpoId), record.GroupPolicyId);
    }

    [Fact]
    public void ForwardedGpoObjectPreservesActorAndTransportProvenance() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        EventObject source = CreateSource(
            5136,
            "WEC01",
            "ForwardedEvents",
            "groupPolicyContainer",
            "displayName",
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz");

        GroupPolicyAuditRecord record = GroupPolicyAuditEngine.CreateRecord(source);

        Assert.Equal(GroupPolicyAuditEventKind.Modified, record.Kind);
        Assert.Equal(GroupPolicyAuditTargetKind.GroupPolicyObject, record.TargetKind);
        Assert.Equal(Guid.Parse(gpoId), record.GroupPolicyId);
        Assert.Equal("AD1.ad.evotec.xyz", record.SourceComputer);
        Assert.Equal("WEC01", record.QueryTarget);
        Assert.Equal("Security", record.OriginalLogName);
        Assert.Equal("ForwardedEvents", record.ContainerLogName);
        Assert.Equal("EVOTEC\\alice", record.Actor);
        Assert.Equal("S-1-5-21-1-2-3-1105", record.ActorSid);
        Assert.Equal("{operation-correlation}", record.OperationCorrelationId);
    }

    [Fact]
    public void GroupPolicyProjectionRetainsSystemActivityMetadata() {
        Guid activityId = Guid.Parse("7e2abf19-c7d2-40a9-8ec9-a5c7168e3c06");
        Guid relatedActivityId = Guid.Parse("4ce61f98-73d6-4e2e-bf3c-3390e20e100d");
        GroupPolicyAuditRecord record = GroupPolicyAuditEngine.CreateRecord(CreateSource(
            5136,
            "WEC01",
            "ForwardedEvents",
            "groupPolicyContainer",
            "displayName",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            activityId: activityId,
            relatedActivityId: relatedActivityId));

        EventReportRow row = EventReportEngine.CreateRow(record);

        Assert.Equal(activityId, record.ActivityId);
        Assert.Equal(relatedActivityId, record.RelatedActivityId);
        Assert.Equal(activityId, row.ActivityId);
        Assert.Equal(relatedActivityId, row.RelatedActivityId);
    }

    [Fact]
    public async Task ReportProjectionKeepsOneSchemaAcrossUnknownAndResolvedContext() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "groupPolicyContainer",
            "displayName",
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            attributeValue: "Domain controllers baseline");
        GroupPolicyAuditRecord unknown = GroupPolicyAuditEngine.CreateRecord(source);
        GroupPolicyAuditRecord resolved = await GroupPolicyAuditEngine.CreateRecordAsync(
            source,
            new InMemoryEventContextStore());

        EventReportProjection unknownProjection = EventReportProjectionFactory.Create(unknown);
        EventReportProjection resolvedProjection = EventReportProjectionFactory.Create(resolved);

        Assert.Equal(unknownProjection.Section.Key, resolvedProjection.Section.Key);
        Assert.Equal("Domain controllers baseline", resolved.GroupPolicyNameAtEventTime);
        Assert.Equal(
            typeof(string),
            unknownProjection.Section.Columns.Single(static column =>
                column.Name == nameof(GroupPolicyAuditRecord.GroupPolicyNameAtEventTime)).ValueType);
    }

    [Fact]
    public async Task GroupPolicyTypeSelectorIncludesThePersistentAuditIdentity() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        GroupPolicyAuditRecord record = GroupPolicyAuditEngine.CreateRecord(CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "groupPolicyContainer",
            "displayName",
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            attributeValue: "Domain controllers baseline"));
        EventReport report = EventReportEngine.Create(new object[] { record });
        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-gpo-store-{Guid.NewGuid():N}.db");
        try {
            Assert.Equal("GroupPolicyAudit", Assert.Single(report.Rows).Type);
            Assert.Equal("GroupPolicyAudit", Assert.Single(report.Sections).Name);
            var store = new EventStore(path);
            await store.WriteAsync(report);

            EventReport restored = await store.ReadReportAsync(new EventStoreQuery {
                Types = new[] { EventType.GroupPolicyDirectoryAudit }
            });

            Assert.Single(restored.Rows);
        } finally {
            foreach (string candidate in new[] { path, path + "-wal", path + "-shm" }) {
                if (File.Exists(candidate)) {
                    File.Delete(candidate);
                }
            }
        }
    }

    [Theory]
    [InlineData("gPLink", GroupPolicyAuditTargetKind.ScopeLinks)]
    [InlineData("gPOptions", GroupPolicyAuditTargetKind.ScopeInheritance)]
    [InlineData("gPCWQLFilter", GroupPolicyAuditTargetKind.WmiFilterAssignment)]
    public void ScopeEventsAreProjectedWithoutDirectoryLookups(
        string attributeName,
        GroupPolicyAuditTargetKind expectedKind) {

        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "organizationalUnit",
            attributeName,
            "OU=Servers,DC=ad,DC=evotec,DC=xyz");

        GroupPolicyAuditRecord record = GroupPolicyAuditEngine.CreateRecord(source);

        Assert.Equal(expectedKind, record.TargetKind);
        Assert.Null(record.GroupPolicyId);
        Assert.Equal("OU=Servers,DC=ad,DC=evotec,DC=xyz", record.ObjectDistinguishedName);
    }

    [Fact]
    public void WmiFilterDefinitionIsProjectedWithoutAGroupPolicyLookup() {
        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "msWMI-Som",
            "msWMI-Parm2",
            "CN={F91E082B-25C8-4D63-9D8C-946B9AB4DF85},CN=SOM,CN=WMIPolicy,CN=System,DC=ad,DC=evotec,DC=xyz");

        GroupPolicyAuditRecord record = GroupPolicyAuditEngine.CreateRecord(source);

        Assert.Equal(GroupPolicyAuditTargetKind.WmiFilterDefinition, record.TargetKind);
        Assert.Null(record.GroupPolicyId);
    }

    [Fact]
    public void GroupPolicyContextFactUsesOnlySelectedEventEvidence() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "groupPolicyContainer",
            "displayName",
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            attributeValue: "Domain controllers baseline");

        GroupPolicyAuditRecord record = GroupPolicyAuditEngine.CreateRecord(source);
        EventContextFact fact = Assert.IsType<EventContextFact>(GroupPolicyContextFactFactory.Create(record));

        Assert.Equal(Guid.Parse(gpoId).ToString("D").ToUpperInvariant(),
            EventContextIdentity.NormalizeCanonicalId(fact.ObjectKind, fact.CanonicalId));
        Assert.Equal("Domain controllers baseline", fact.DisplayName);
        Assert.Equal("ad.evotec.xyz", fact.Domain);
        Assert.Equal(EventContextProvenance.Event, fact.Provenance);
        Assert.True(fact.IsShareable);
    }

    [Fact]
    public async Task WmiFilterAssignmentOnAGroupPolicyContainerRetainsContext() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        string distinguishedName =
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz";
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(new EventContextFact {
            ObjectKind = EventContextObjectKind.GroupPolicy,
            CanonicalId = gpoId,
            Aliases = new[] { distinguishedName },
            DisplayName = "Existing policy",
            Domain = "ad.evotec.xyz",
            DistinguishedName = distinguishedName,
            EffectiveAtUtc = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc),
            ObservedAtUtc = new DateTime(2026, 8, 18, 9, 1, 0, DateTimeKind.Utc),
            Provenance = EventContextProvenance.Event,
            SourceIdentity = "existing-policy",
            ProviderName = "EventViewerX.Tests",
            ProviderSchemaVersion = 1,
            IsShareable = true
        });
        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "groupPolicyContainer",
            "gPCWQLFilter",
            distinguishedName,
            attributeValue: "[ad.evotec.xyz;{F91E082B-25C8-4D63-9D8C-946B9AB4DF85};0]");

        GroupPolicyAuditRecord record = await GroupPolicyAuditEngine.CreateRecordAsync(source, store);
        EventContextFact fact = Assert.IsType<EventContextFact>(GroupPolicyContextFactFactory.Create(record));

        Assert.Equal(GroupPolicyAuditTargetKind.WmiFilterAssignment, record.TargetKind);
        Assert.Equal(Guid.Parse(gpoId), record.GroupPolicyId);
        Assert.Equal("Existing policy", record.GroupPolicyNameAtEventTime);
        Assert.Equal("Existing policy", record.GroupPolicyCurrentName);
        Assert.Equal(Guid.Parse(gpoId).ToString("D").ToUpperInvariant(),
            EventContextIdentity.NormalizeCanonicalId(fact.ObjectKind, fact.CanonicalId));
    }

    [Fact]
    public void DeletedAttributeValueIsNotMistakenForTheCurrentGpoName() {
        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "groupPolicyContainer",
            "displayName",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            attributeValue: "Retired name",
            operationType: "%%14675");

        EventContextFact fact = Assert.IsType<EventContextFact>(
            GroupPolicyContextFactFactory.Create(GroupPolicyAuditEngine.CreateRecord(source)));

        Assert.Null(fact.DisplayName);
        Assert.True(fact.DisplayNameObserved);
    }

    [Fact]
    public void SnapshotRetainsTheExplicitContextStore() {
        var store = new InMemoryEventContextStore();
        var query = new GroupPolicyAuditQuery {
            ContextStore = store,
            AuthorizationContext = " evotec\\reader-a "
        };

        GroupPolicyAuditQuery snapshot = GroupPolicyAuditEngine.CreateSnapshot(query);

        Assert.Same(store, snapshot.ContextStore);
        Assert.Equal("EVOTEC\\READER-A", snapshot.AuthorizationContext);
    }

    [Fact]
    public async Task MaterializedEventResolvesCallerAuthorizedNonShareableContext() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        string distinguishedName =
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz";
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(new EventContextFact {
            ObjectKind = EventContextObjectKind.GroupPolicy,
            CanonicalId = gpoId,
            Aliases = new[] { distinguishedName },
            DisplayName = "Restricted policy",
            Domain = "ad.evotec.xyz",
            DistinguishedName = distinguishedName,
            EffectiveAtUtc = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc),
            ObservedAtUtc = new DateTime(2026, 8, 18, 9, 1, 0, DateTimeKind.Utc),
            Provenance = EventContextProvenance.LiveLookup,
            SourceIdentity = "restricted-lookup",
            ProviderName = "EventViewerX.Tests",
            ProviderSchemaVersion = 1,
            AuthorizationContext = "EVOTEC\\reader-a",
            IsShareable = false
        });
        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "groupPolicyContainer",
            "versionNumber",
            distinguishedName,
            attributeValue: "2");

        GroupPolicyAuditRecord denied = await GroupPolicyAuditEngine.CreateRecordAsync(source, store);
        GroupPolicyAuditRecord allowed = await GroupPolicyAuditEngine.CreateRecordAsync(
            source,
            store,
            cancellationToken: default,
            authorizationContext: "evotec\\READER-A");

        Assert.Null(denied.GroupPolicyNameAtEventTime);
        Assert.Equal("Restricted policy", allowed.GroupPolicyNameAtEventTime);
        Assert.Equal("Restricted policy", allowed.GroupPolicyCurrentName);
    }

    [Fact]
    public async Task MaterializedEventCanPopulateAndResolveExplicitContext() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "groupPolicyContainer",
            "displayName",
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            attributeValue: "Domain controllers baseline");

        GroupPolicyAuditRecord record = await GroupPolicyAuditEngine.CreateRecordAsync(
            source,
            new InMemoryEventContextStore(),
            default);

        Assert.Equal(EventContextState.Current, record.ContextState);
        Assert.Equal("Domain controllers baseline", record.GroupPolicyNameAtEventTime);
        Assert.Equal("Domain controllers baseline", record.GroupPolicyCurrentName);
    }

    [Fact]
    public async Task TimelineIsFullyIngestedBeforeRecordsAreResolved() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        string distinguishedName =
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz";
        DateTime createdUtc = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
        DateTime renamedUtc = createdUtc.AddHours(1);
        DateTime deletedUtc = createdUtc.AddHours(2);
        GroupPolicyAuditRecord[] records = {
            GroupPolicyAuditEngine.CreateRecord(CreateSource(
                5137,
                "AD1.ad.evotec.xyz",
                "Security",
                "groupPolicyContainer",
                "displayName",
                distinguishedName,
                attributeValue: "Original policy",
                timeCreatedUtc: createdUtc)),
            GroupPolicyAuditEngine.CreateRecord(CreateSource(
                5136,
                "AD1.ad.evotec.xyz",
                "Security",
                "groupPolicyContainer",
                "displayName",
                distinguishedName,
                attributeValue: "Renamed policy",
                timeCreatedUtc: renamedUtc)),
            GroupPolicyAuditEngine.CreateRecord(CreateSource(
                5141,
                "AD1.ad.evotec.xyz",
                "Security",
                "groupPolicyContainer",
                string.Empty,
                distinguishedName,
                attributeValue: "-",
                operationType: string.Empty,
                timeCreatedUtc: deletedUtc))
        };

        var store = new BatchOnlyContextStore();
        await GroupPolicyAuditEngine.FinalizeContextAsync(records, store);

        Assert.Equal(EventContextState.Historical, records[0].ContextState);
        Assert.Equal("Original policy", records[0].GroupPolicyNameAtEventTime);
        Assert.Null(records[0].GroupPolicyCurrentName);
        Assert.Equal(EventContextState.Historical, records[1].ContextState);
        Assert.Equal("Renamed policy", records[1].GroupPolicyNameAtEventTime);
        Assert.Equal(EventContextState.Deleted, records[2].ContextState);
        Assert.Equal("Renamed policy", records[2].GroupPolicyLastKnownName);
        Assert.Null(records[2].GroupPolicyCurrentName);
        Assert.Equal(1, store.StoreManyCalls);
        Assert.Equal(1, store.ResolveManyCalls);
    }

    [Fact]
    public async Task BufferedFinalizationResolvesCallerAuthorizedContext() {
        const string gpoId = "FB6A0E91-F93D-4428-B29D-2FDCC3A95425";
        string distinguishedName =
            $"CN={{{gpoId}}},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz";
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(new EventContextFact {
            ObjectKind = EventContextObjectKind.GroupPolicy,
            CanonicalId = gpoId,
            Aliases = new[] { distinguishedName },
            DisplayName = "Buffered restricted policy",
            Domain = "ad.evotec.xyz",
            DistinguishedName = distinguishedName,
            EffectiveAtUtc = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc),
            ObservedAtUtc = new DateTime(2026, 8, 18, 9, 1, 0, DateTimeKind.Utc),
            Provenance = EventContextProvenance.LiveLookup,
            SourceIdentity = "buffered-restricted-lookup",
            ProviderName = "EventViewerX.Tests",
            ProviderSchemaVersion = 1,
            AuthorizationContext = "EVOTEC\\reader-a",
            IsShareable = false
        });
        GroupPolicyAuditRecord record = GroupPolicyAuditEngine.CreateRecord(CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "groupPolicyContainer",
            "versionNumber",
            distinguishedName,
            attributeValue: "3"));

        await GroupPolicyAuditEngine.FinalizeContextAsync(
            new[] { record },
            store,
            "evotec\\READER-A");

        Assert.Equal("Buffered restricted policy", record.GroupPolicyNameAtEventTime);
        Assert.Equal("Buffered restricted policy", record.GroupPolicyCurrentName);
    }

    [Fact]
    public void UnrelatedDirectoryEventIsRejected() {
        EventObject source = CreateSource(
            5136,
            "AD1.ad.evotec.xyz",
            "Security",
            "user",
            "displayName",
            "CN=Alice,OU=Users,DC=ad,DC=evotec,DC=xyz");

        Assert.Throws<ArgumentException>(() => GroupPolicyAuditEngine.CreateRecord(source));
    }

    [Theory]
    [InlineData(5138, "Microsoft-Windows-Security-Auditing", "Security")]
    [InlineData(5136, "Other-Provider", "Security")]
    [InlineData(5136, "Microsoft-Windows-Security-Auditing", "System")]
    public void EventsOutsideTheSecuritySourceContractAreRejected(
        int eventId,
        string providerName,
        string originalLogName) {

        EventObject source = CreateSource(
            eventId,
            "WEC01",
            "ForwardedEvents",
            "groupPolicyContainer",
            "displayName",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            providerName,
            originalLogName);

        Assert.Throws<ArgumentException>(() => GroupPolicyAuditEngine.CreateRecord(source));
    }

    [Fact]
    public void SnapshotFreezesCheckpointAndCollectorSettings() {
        var checkpoint = new GroupPolicyAuditCheckpoint {
            QueryTarget = " WEC01 ",
            ContainerLogName = " ForwardedEvents ",
            BookmarkXml = "<BookmarkList />"
        };
        var query = new GroupPolicyAuditQuery {
            MachineNames = new string?[] { "WEC01" },
            CollectorLogName = " ForwardedEvents ",
            Checkpoints = new[] { checkpoint },
            MaxCandidates = 5000
        };

        GroupPolicyAuditQuery snapshot = GroupPolicyAuditEngine.CreateSnapshot(query);
        checkpoint.BookmarkXml = "changed";
        query.CollectorLogName = "Other";

        Assert.Equal("ForwardedEvents", snapshot.CollectorLogName);
        Assert.Equal("<BookmarkList />", Assert.Single(snapshot.Checkpoints!).BookmarkXml);
        Assert.Equal(5000, snapshot.MaxCandidates);
        Assert.True(snapshot.Oldest);
    }

    [Fact]
    public void SnapshotRejectsCheckpointCapturedWithOppositeOrdering() {
        var query = new GroupPolicyAuditQuery {
            Oldest = false,
            Checkpoints = new[] {
                new GroupPolicyAuditCheckpoint {
                    QueryTarget = "WEC01",
                    ContainerLogName = "ForwardedEvents",
                    BookmarkXml = "<BookmarkList />",
                    Oldest = true
                }
            }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => GroupPolicyAuditEngine.CreateSnapshot(query));

        Assert.Contains("Oldest", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionInfoReturnsDetachedCheckpointSnapshots() {
        const string bookmark =
            "<BookmarkList><Bookmark Channel='Security' RecordId='42' IsCurrent='true'/></BookmarkList>";
        EventObject source = CreateSource(
            5136,
            "WEC01",
            "ForwardedEvents",
            "groupPolicyContainer",
            "displayName",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            bookmarkXml: bookmark);
        var info = new GroupPolicyAuditQueryExecutionInfo();
        info.RecordCheckpoint(source, oldest: true);

        GroupPolicyAuditCheckpoint returned = Assert.Single(info.Checkpoints);
        returned.BookmarkXml = "changed";
        returned.Oldest = false;

        GroupPolicyAuditCheckpoint current = Assert.Single(info.Checkpoints);
        Assert.Equal(bookmark, current.BookmarkXml);
        Assert.True(current.Oldest);
    }

    [Fact]
    public async Task BufferedCancellationDoesNotAdvanceCheckpointBeyondDeliveredRecord() {
        const string firstBookmark =
            "<BookmarkList><Bookmark Channel='Security' RecordId='41' IsCurrent='true'/></BookmarkList>";
        const string secondBookmark =
            "<BookmarkList><Bookmark Channel='Security' RecordId='42' IsCurrent='true'/></BookmarkList>";
        EventObject firstSource = CreateSource(
            5136,
            "WEC01",
            "ForwardedEvents",
            "groupPolicyContainer",
            "displayName",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            bookmarkXml: firstBookmark,
            timeCreatedUtc: new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc));
        EventObject secondSource = CreateSource(
            5136,
            "WEC01",
            "ForwardedEvents",
            "groupPolicyContainer",
            "displayName",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            bookmarkXml: secondBookmark,
            timeCreatedUtc: new DateTime(2026, 8, 18, 10, 1, 0, DateTimeKind.Utc));
        var captured = new GroupPolicyAuditQueryExecutionInfo();
        captured.RecordCheckpoint(firstSource, oldest: true);
        var first = new GroupPolicyAuditBufferedRecord(
            GroupPolicyAuditEngine.CreateRecord(firstSource),
            captured.Checkpoints);
        captured.RecordCheckpoint(secondSource, oldest: true);
        var second = new GroupPolicyAuditBufferedRecord(
            GroupPolicyAuditEngine.CreateRecord(secondSource),
            captured.Checkpoints);
        var delivered = new GroupPolicyAuditQueryExecutionInfo();
        using var cancellation = new CancellationTokenSource();
        await using IAsyncEnumerator<GroupPolicyAuditRecord> enumerator = GroupPolicyAuditEngine
            .DeliverBufferedAsync(
                new[] { first, second },
                captured.Checkpoints,
                delivered,
                cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, delivered.EventsEmitted);
        Assert.Equal(firstBookmark, Assert.Single(delivered.Checkpoints).BookmarkXml);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await enumerator.MoveNextAsync().AsTask());

        Assert.Equal(1, delivered.EventsEmitted);
        Assert.Equal(firstBookmark, Assert.Single(delivered.Checkpoints).BookmarkXml);
    }

    [Fact]
    public void DeclarativeAndTypedQueriesFreezeBookmarkResolversAndOptions() {
        Func<string?, string, string?> resolver = static (_, _) => "<BookmarkList />";
        var definitionQuery = new EventDefinitionQuery(GroupPolicyAuditDefinitions.CreateDirectoryChanges()) {
            BookmarkXmlResolver = resolver,
            BookmarkOffset = 2,
            StrictBookmark = false
        };
        var typeQuery = new EventTypeQuery(new[] { EventType.ADGroupPolicyChangesDetailed }) {
            BookmarkXmlResolver = resolver,
            BookmarkOffset = 2,
            StrictBookmark = false
        };

        EventDefinitionQuery definitionSnapshot = EventDefinitionEngine.CreateSnapshot(definitionQuery);
        EventTypeQuery typeSnapshot = EventTypeQuerySnapshot.Copy(typeQuery);

        Assert.Same(resolver, definitionSnapshot.BookmarkXmlResolver);
        Assert.Equal(2, definitionSnapshot.BookmarkOffset);
        Assert.False(definitionSnapshot.StrictBookmark);
        Assert.Same(resolver, typeSnapshot.BookmarkXmlResolver);
        Assert.Equal(2, typeSnapshot.BookmarkOffset);
        Assert.False(typeSnapshot.StrictBookmark);
    }

    [Fact]
    public void CheckpointSourceKey_NormalizesEveryLocalMachineSpelling() {
        string expected = GroupPolicyAuditCheckpoint.CreateSourceKey(
            Environment.MachineName,
            "Security");

        Assert.Equal(expected, GroupPolicyAuditCheckpoint.CreateSourceKey(null, "Security"));
        Assert.Equal(expected, GroupPolicyAuditCheckpoint.CreateSourceKey(".", "Security"));
        Assert.Equal(expected, GroupPolicyAuditCheckpoint.CreateSourceKey("localhost", "Security"));
        Assert.Equal(expected, GroupPolicyAuditCheckpoint.CreateSourceKey(
            EventLogTarget.LocalMachineName,
            "Security"));
    }

    [Fact]
    public void ReportCoveragePreservesCollectorLogAndOfflineFailures() {
        string offline = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.evtx");
        var offlineQuery = new GroupPolicyAuditQuery { Paths = new[] { offline } };
        var offlineFailure = new EventLogQueryTargetFailure(
            "Offline",
            offline,
            EventLogRemoteQueryFailureKind.LogNotFound,
            "Missing fixture.");
        var collectorQuery = new GroupPolicyAuditQuery {
            MachineNames = new[] { "wec01" },
            CollectorLogName = "ForwardedEvents"
        };
        var collectorFailure = new EventLogQueryTargetFailure(
            "wec01",
            "ForwardedEvents",
            EventLogRemoteQueryFailureKind.AccessDenied,
            "Denied.");

        EventReportCoverage offlineCoverage = Assert.Single(
            GroupPolicyAuditReportEngine.BuildCoverage(offlineQuery, new[] { offlineFailure }));
        EventReportCoverage collectorCoverage = Assert.Single(
            GroupPolicyAuditReportEngine.BuildCoverage(collectorQuery, new[] { collectorFailure }));

        Assert.False(offlineCoverage.Succeeded);
        Assert.Equal(EventLogRemoteQueryFailureKind.LogNotFound.ToString(), offlineCoverage.Status);
        Assert.Equal(Path.GetFullPath(offline), offlineCoverage.LogName);
        Assert.False(collectorCoverage.Succeeded);
        Assert.Equal("ForwardedEvents", collectorCoverage.LogName);
        Assert.Equal(EventLogRemoteQueryFailureKind.AccessDenied.ToString(), collectorCoverage.Status);
    }

    [Fact]
    public void EmptyMachineSelectionRetainsLocalFailureCoverage() {
        var query = new GroupPolicyAuditQuery {
            MachineNames = Array.Empty<string?>()
        };
        var failure = new EventLogQueryTargetFailure(
            Environment.MachineName,
            "Security",
            EventLogRemoteQueryFailureKind.AccessDenied,
            "Denied.");

        EventReportCoverage coverage = Assert.Single(
            GroupPolicyAuditReportEngine.BuildCoverage(query, new[] { failure }));

        Assert.Equal(Environment.MachineName, coverage.MachineName, ignoreCase: true);
        Assert.False(coverage.Succeeded);
        Assert.Equal(EventLogRemoteQueryFailureKind.AccessDenied.ToString(), coverage.Status);
    }

    private static EventObject CreateSource(
        int eventId,
        string queriedMachine,
        string container,
        string objectClass,
        string attributeName,
        string objectDn,
        string providerName = "Microsoft-Windows-Security-Auditing",
        string originalLogName = "Security",
        string? bookmarkXml = null,
        string attributeValue = "value",
        string operationType = "%%14674",
        DateTime? timeCreatedUtc = null,
        Guid? activityId = null,
        Guid? relatedActivityId = null) {

        string xml = $$"""
            <Event>
              <EventData>
                <Data Name="OpCorrelationID">{operation-correlation}</Data>
                <Data Name="AppCorrelationID">-</Data>
                <Data Name="SubjectUserSid">S-1-5-21-1-2-3-1105</Data>
                <Data Name="SubjectUserName">alice</Data>
                <Data Name="SubjectDomainName">EVOTEC</Data>
                <Data Name="SubjectLogonId">0x1234</Data>
                <Data Name="DSName">ad.evotec.xyz</Data>
                <Data Name="DSType">%%14676</Data>
                <Data Name="ObjectDN">{{objectDn}}</Data>
                <Data Name="ObjectGUID">{9b263379-4310-4585-9eb3-ee688590d3f0}</Data>
                <Data Name="ObjectClass">{{objectClass}}</Data>
                <Data Name="AttributeLDAPDisplayName">{{attributeName}}</Data>
                <Data Name="AttributeValue">{{attributeValue}}</Data>
                <Data Name="OperationType">{{operationType}}</Data>
              </EventData>
            </Event>
            """;
        return CreateSourceFromXml(
            eventId,
            queriedMachine,
            container,
            xml,
            providerName,
            originalLogName,
            bookmarkXml,
            timeCreatedUtc,
            activityId,
            relatedActivityId);
    }

    private static EventObject CreateMovedSource(string oldObjectDn, string newObjectDn) {
        string xml = $$"""
            <Event>
              <EventData>
                <Data Name="OpCorrelationID">{operation-correlation}</Data>
                <Data Name="AppCorrelationID">-</Data>
                <Data Name="SubjectUserSid">S-1-5-21-1-2-3-1105</Data>
                <Data Name="SubjectUserName">alice</Data>
                <Data Name="SubjectDomainName">EVOTEC</Data>
                <Data Name="SubjectLogonId">0x1234</Data>
                <Data Name="DSName">ad.evotec.xyz</Data>
                <Data Name="DSType">%%14676</Data>
                <Data Name="OldObjectDN">{{oldObjectDn}}</Data>
                <Data Name="NewObjectDN">{{newObjectDn}}</Data>
                <Data Name="ObjectGUID">{9b263379-4310-4585-9eb3-ee688590d3f0}</Data>
                <Data Name="ObjectClass">groupPolicyContainer</Data>
              </EventData>
            </Event>
            """;
        return CreateSourceFromXml(
            5139,
            "WEC01",
            "ForwardedEvents",
            xml,
            "Microsoft-Windows-Security-Auditing",
            "Security",
            null,
            null);
    }

    private static EventObject CreateSourceFromXml(
        int eventId,
        string queriedMachine,
        string container,
        string xml,
        string providerName,
        string originalLogName,
        string? bookmarkXml,
        DateTime? timeCreatedUtc,
        Guid? activityId = null,
        Guid? relatedActivityId = null) {

        return new EventObject(
            new SyntheticEventRecord(
                eventId,
                xml,
                providerName,
                originalLogName,
                bookmarkXml,
                timeCreatedUtc,
                activityId,
                relatedActivityId),
            queriedMachine,
            EventReadMode.StructuredData,
            includeBookmark: !string.IsNullOrWhiteSpace(bookmarkXml)) {
            ContainerLog = container,
            GatheredLogName = container
        };
    }

    private sealed class SyntheticEventRecord : EventRecord {
        private readonly int _eventId;
        private readonly string _xml;
        private readonly string _providerName;
        private readonly string _logName;
        private readonly string? _bookmarkXml;
        private readonly DateTime _timeCreatedUtc;
        private readonly Guid? _activityId;
        private readonly Guid? _relatedActivityId;

        internal SyntheticEventRecord(
            int eventId,
            string xml,
            string providerName,
            string logName,
            string? bookmarkXml,
            DateTime? timeCreatedUtc,
            Guid? activityId,
            Guid? relatedActivityId) {

            _eventId = eventId;
            _xml = xml;
            _providerName = providerName;
            _logName = logName;
            _bookmarkXml = bookmarkXml;
            _timeCreatedUtc = timeCreatedUtc ?? new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
            _activityId = activityId;
            _relatedActivityId = relatedActivityId;
        }

        public override string ProviderName => _providerName;
        public override string LogName => _logName;
        public override string MachineName => "AD1.ad.evotec.xyz";
        public override int Id => _eventId;
        public override byte? Level => 0;
        public override int? Task => 14081;
        public override long? Keywords => 0;
        public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();
        public override short? Opcode => 0;
        public override string OpcodeDisplayName => string.Empty;
        public override string TaskDisplayName => string.Empty;
        public override Guid? ProviderId => null;
        public override Guid? ActivityId => _activityId;
        public override Guid? RelatedActivityId => _relatedActivityId;
        public override int? ProcessId => 1;
        public override int? ThreadId => 1;
        public override string LevelDisplayName => "Information";
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => _timeCreatedUtc;
        public override int? Qualifiers => null;
        public override long? RecordId => 42;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => string.IsNullOrWhiteSpace(_bookmarkXml)
            ? null!
            : new EventBookmark(_bookmarkXml);
        public override string FormatDescription() => string.Empty;
        public override string FormatDescription(IEnumerable<object> values) => string.Empty;
        public override string ToXml() => _xml;
    }

    private sealed class BatchOnlyContextStore : IEventContextStore {
        private readonly InMemoryEventContextStore _inner = new();

        internal int StoreManyCalls { get; private set; }
        internal int ResolveManyCalls { get; private set; }

        public ValueTask StoreAsync(EventContextFact fact, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Buffered finalization must use StoreManyAsync.");

        public async ValueTask StoreManyAsync(
            IReadOnlyList<EventContextFact> facts,
            CancellationToken cancellationToken = default) {

            StoreManyCalls++;
            await _inner.StoreManyAsync(facts, cancellationToken);
        }

        public ValueTask<EventContextResolution> ResolveAsync(
            EventContextQuery query,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Buffered finalization must use ResolveManyAsync.");

        public async ValueTask<IReadOnlyList<EventContextResolution>> ResolveManyAsync(
            IReadOnlyList<EventContextQuery> queries,
            CancellationToken cancellationToken = default) {

            ResolveManyCalls++;
            return await _inner.ResolveManyAsync(queries, cancellationToken);
        }
    }
}
