using Xunit;

namespace EventViewerX.Tests;

public sealed class TestUnavailableEventSources {
    [Theory]
    [InlineData(EventType.DhcpLeaseCreated)]
    [InlineData(EventType.SqlDatabaseCreated)]
    [InlineData(EventType.NetworkPromiscuousMode)]
    [InlineData(EventType.HyperVCheckpointCreated)]
    [InlineData(EventType.AADConnectRunProfile)]
    public void QueryCompilationRejectsSourcesThatDoNotEstablishTheAdvertisedEvent(EventType type) {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            EventDefinitionCompiler.BuildQueryXml(new[] { type }));
        Assert.Contains(type.ToString(), error.Message);
        EventTypeDefinition definition = EventTypeCatalog.GetDefinition(type);
        Assert.Contains(type.ToString(), definition.UnavailableReason);
        Assert.Empty(definition.Sources);
        Assert.NotEmpty(definition.Fields);
        Assert.Contains(EventRequirementCatalog.GetRequirement(type).Prerequisites,
            item => item.Key == "unavailable-source:" + type && item.Description == definition.UnavailableReason);
    }
    [Theory]
    [InlineData(EventType.NetworkSecurity, "NetworkPromiscuousMode")]
    [InlineData(EventType.InfrastructureHealth, "SqlDatabaseCreated")]
    [InlineData(EventType.EntraConnectHealth, "AADConnectRunProfile")]
    public void CompositeQueryCannotSilentlyOmitUnavailableMembers(EventType type, string member) {
        Assert.Contains(member, EventTypeCatalog.GetDefinition(type).UnavailableReason);
        Assert.Contains(member, Assert.Throws<NotSupportedException>(() =>
            EventTypeCatalog.GetSources(new[] { type })).Message);
    }

    [Theory]
    [InlineData(17137)]
    [InlineData(1802)]
    public void SqlStartupAndCreationFailureCannotBecomeCreatedRecords(int eventId) {
        EventObject source = new SavedEventRecord {
            EventId = eventId, Channel = "Application", ProviderName = "MSSQLSERVER",
            Computer = "sql-fixture", TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc)
        }.ToEventObject("sql-fixture", EventReadMode.StructuredData);
        Assert.Null(EventTypeCatalog.CreateEventRule(source, new[] { EventType.SqlDatabaseCreated }));
    }
    [Fact]
    public void CoverageInspectionDescribesUnavailableRequirementsWithoutOpeningASource() {
        EventDetectionPack pack = EventDetectionPack.Create("source-support-test", "1.0.0", new[] {
            new EventDetectionRuleDefinition { RuleId = "historical-sql", Title = "Historical SQL identity",
                EventTypes = new[] { EventType.SqlDatabaseCreated } }
        });
        EventDetectionPackCoverage coverage = pack.GetCoverage();
        Assert.Contains(EventType.SqlDatabaseCreated, coverage.EventTypes);
        Assert.Empty(coverage.EventIds);
        Assert.Empty(coverage.Channels);
        Assert.Contains(coverage.Prerequisites, item => item.Key == "unavailable-source:SqlDatabaseCreated");
    }
}