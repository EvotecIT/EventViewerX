using System.Net;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public class TestQueryBuilders {
    [Fact]
    public void EventQueryBuilderNormalizesAndDetachesInputs() {
        var eventIds = new[] { 4624, 4625 };
        var machines = new string?[] { "server01.contoso.test" };
        var credential = new NetworkCredential("reader", "secret", "CONTOSO");
        var builder = new EventQueryDefinitionBuilder {
            Filter = new EventFilter { EventIds = eventIds },
            MachineNames = machines
        };
        builder.FromChannels(" System ", "system", "Application");
        builder.Options.Credential = credential;
        builder.Options.MaxEvents = 25;

        EventQueryDefinition query = builder.Build();
        eventIds[0] = 1;
        machines[0] = "changed.contoso.test";
        credential.UserName = "changed";
        builder.Filter.EventIds = new[] { 2 };

        Assert.Equal(new[] { "System", "Application" }, query.LogNames);
        Assert.Equal(new[] { 4624, 4625 }, query.Filter!.EventIds);
        Assert.Equal("server01.contoso.test", Assert.Single(query.MachineNames!));
        Assert.Equal("reader", query.Options.Credential!.UserName);
        Assert.Equal("CONTOSO", query.Options.Credential.Domain);
        Assert.Equal(25, query.Options.MaxEvents);
    }

    [Fact]
    public void EventQueryBuilderRejectsIncompleteAndConflictingRequests() {
        Assert.Throws<InvalidOperationException>(() => new EventQueryDefinitionBuilder().Build());

        var conflicting = new EventQueryDefinitionBuilder {
            Filter = new EventFilter { EventIds = new[] { 4624 } },
            FilterXPath = "*[System[Level=2]]"
        };
        conflicting.FromChannels("Security");
        Assert.Throws<InvalidOperationException>(() => conflicting.Build());

        var invalidOptions = new EventQueryDefinitionBuilder();
        invalidOptions.FromChannels("Security");
        invalidOptions.Options.MaxConcurrency = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidOptions.Build());
    }

    [Fact]
    public void CollectorChannelFactoryTargetsForwardedEventsAndPreservesTheOriginalChannel() {
        EventLogChannelQuery query = EventLogChannelQuery.ForCollector(
            "Security",
            "WEC01",
            "*[System[EventID=4624]]");

        Assert.Equal("ForwardedEvents", query.LogName);
        Assert.Equal("WEC01", query.MachineName);
        Assert.Contains("Channel='Security'", query.XPath, StringComparison.Ordinal);
        Assert.Contains("EventID=4624", query.XPath, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreQueryBuilderNormalizesAndDetachesInputs() {
        var types = new[] { EventType.ADUserLogonFailed, EventType.ADUserLogonFailed };
        var computers = new[] { " SERVER01 ", "server01", "SERVER02" };
        EventPredicate predicate = EventPredicate.Compare(
            "Who",
            EventPredicateOperator.Equal,
            "operator");
        var builder = new EventStoreQueryBuilder {
            Types = types,
            SourceComputers = computers,
            Predicate = predicate,
            StartTime = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc),
            MaxEvents = 50
        };

        EventStoreQuery query = builder.Build();
        types[0] = EventType.ADUserCreateChange;
        computers[0] = "changed";
        predicate.Field = "Changed";

        Assert.Equal(new[] { EventType.ADUserLogonFailed }, query.Types);
        Assert.Equal(new[] { "SERVER01", "SERVER02" }, query.SourceComputers);
        Assert.Equal("Who", query.Predicate!.Field);
        Assert.Equal(DateTimeKind.Utc, query.StartTime!.Value.Kind);
        Assert.Equal(50, query.MaxEvents);
    }

    [Fact]
    public void FindingQueryBuilderNormalizesAndValidatesEntitySelection() {
        var ruleIds = new[] { " rule.one ", "RULE.ONE", "rule.two" };
        var builder = new EventFindingStoreQueryBuilder {
            RuleIds = ruleIds,
            EntityField = " Account ",
            EntityValue = " Alice ",
            MaxFindings = 20
        };

        EventFindingStoreQuery query = builder.Build();
        ruleIds[0] = "changed";

        Assert.Equal(new[] { "rule.one", "rule.two" }, query.RuleIds);
        Assert.Equal("Account", query.EntityField);
        Assert.Equal("Alice", query.EntityValue);
        Assert.Equal(20, query.MaxFindings);

        builder.EntityValue = null;
        Assert.Throws<ArgumentException>(() => builder.Build());
    }
}
