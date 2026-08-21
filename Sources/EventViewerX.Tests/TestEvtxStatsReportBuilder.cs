using System;
using EventViewerX.Reports.Stats;
using Xunit;

namespace EventViewerX.Tests;

public class TestEvtxStatsReportBuilder {
    [Fact]
    public void TestCountsAndMinMaxUtc() {
        var b = new EvtxStatsReportBuilder();

        b.Add(
            id: 4624,
            timeCreatedUtc: new DateTime(2026, 02, 10, 10, 00, 00, DateTimeKind.Utc),
            providerName: "Microsoft-Windows-Security-Auditing",
            computerName: "DC01",
            level: 4,
            levelDisplayName: "Information");

        b.Add(
            id: 4625,
            timeCreatedUtc: new DateTime(2026, 02, 10, 11, 00, 00, DateTimeKind.Utc),
            providerName: "Microsoft-Windows-Security-Auditing",
            computerName: "DC01",
            level: 2,
            levelDisplayName: "Error");

        b.Add(
            id: 4624,
            timeCreatedUtc: new DateTime(2026, 02, 10, 09, 30, 00, DateTimeKind.Utc),
            providerName: "Microsoft-Windows-Security-Auditing",
            computerName: "DC02",
            level: 4,
            levelDisplayName: "Information");

        Assert.Equal(3, b.Scanned);
        Assert.Equal(new DateTime(2026, 02, 10, 09, 30, 00, DateTimeKind.Utc), b.MinUtc);
        Assert.Equal(new DateTime(2026, 02, 10, 11, 00, 00, DateTimeKind.Utc), b.MaxUtc);

        var report = b.Build();
        Assert.Equal(3, report.Scanned);
        Assert.Equal(2, report.ByEventId.Count);
        Assert.Equal(2, report.ByEventId[4624]);
        Assert.Equal(1, report.ByEventId[4625]);

        Assert.Equal(2, report.ByComputerName.Count);
        Assert.Equal(2, report.ByComputerName["DC01"]);
        Assert.Equal(1, report.ByComputerName["DC02"]);

        Assert.Single(report.ByProviderName);
        Assert.Equal(3, report.ByProviderName["Microsoft-Windows-Security-Auditing"]);

        Assert.Equal(2, report.ByLevel.Count);
        Assert.Equal(2, report.ByLevel[4].Count);
        Assert.Equal(1, report.ByLevel[2].Count);

        var topIds = b.GetTopEventIds(10);
        Assert.Equal(2, topIds.Count);
        Assert.Equal(4624, topIds[0].Key);
        Assert.Equal(2, topIds[0].Value);

        var topComputers = b.GetTopComputers(10);
        Assert.Equal(2, topComputers.Count);
        Assert.Equal("DC01", topComputers[0].Key);
        Assert.Equal(2, topComputers[0].Value);

        var topLevels = b.GetTopLevels(10);
        Assert.Equal(2, topLevels.Count);
        Assert.Equal(4, topLevels[0].Level);
        Assert.Equal(2, topLevels[0].Count);
    }

    [Fact]
    public void MissingTimestampsCountWithoutChangingExtrema() {
        var builder =
            new EvtxStatsReportBuilder();
        DateTime valid =
            new(
                2026,
                7,
                25,
                12,
                0,
                0,
                DateTimeKind.Utc);

        builder.Add(
            id: 1,
            timeCreatedUtc: DateTime.MinValue,
            providerName: "Provider",
            computerName: "Computer",
            level: 4,
            levelDisplayName: "Information");
        builder.Add(
            id: 2,
            timeCreatedUtc: valid,
            providerName: "Provider",
            computerName: "Computer",
            level: 4,
            levelDisplayName: "Information");

        Assert.Equal(2, builder.Scanned);
        Assert.Equal(valid, builder.MinUtc);
        Assert.Equal(valid, builder.MaxUtc);
    }

    [Fact]
    public void BuiltReportDoesNotChangeWhenBuilderIsReused() {
        var builder =
            new EvtxStatsReportBuilder();
        builder.Add(
            id: 1,
            timeCreatedUtc: DateTime.UtcNow,
            providerName: "Provider",
            computerName: "Computer",
            level: 4,
            levelDisplayName: "Information");
        EvtxStatsReport first =
            builder.Build();

        builder.Add(
            id: 2,
            timeCreatedUtc: DateTime.UtcNow,
            providerName: "Provider",
            computerName: "Computer",
            level: 4,
            levelDisplayName: "Information");

        Assert.Equal(1, first.Scanned);
        Assert.Single(first.ByEventId);
        Assert.Equal(1, first.ByEventId[1]);
        Assert.Equal(1, first.ByLevel[4].Count);
    }
}
