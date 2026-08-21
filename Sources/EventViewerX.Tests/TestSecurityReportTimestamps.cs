using EventViewerX.Native;
using EventViewerX.Reports.Security;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestSecurityReportTimestamps {
    [Fact]
    public void MissingTimestampsDoNotBecomeSecurityReportBounds() {
        EventObject missing = CreateEvent(
            4740,
            DateTime.MinValue);
        EventObject valid = CreateEvent(
            4740,
            new DateTime(
                2026,
                7,
                25,
                12,
                0,
                0,
                DateTimeKind.Utc));
        var builder =
            new SecurityAccountLockoutsReportBuilder(
                includeSamples: true,
                sampleSize: 2);

        builder.Add(missing);
        builder.Add(valid);
        SecurityAccountLockoutsReport report =
            builder.Build();
        builder.Add(
            CreateEvent(
                4740,
                valid.TimeCreated.AddMinutes(1)));

        Assert.Equal(2, report.Matched);
        Assert.Equal(2, report.Samples.Count);
        Assert.Equal(valid.TimeCreated, report.MinUtc);
        Assert.Equal(valid.TimeCreated, report.MaxUtc);
        Assert.Null(report.Samples[0].TimeCreatedUtc);
        Assert.Equal(
            valid.TimeCreated,
            report.Samples[1].TimeCreatedUtc);
    }

    [Fact]
    public void AllSecurityBuildersOmitMissingTimestampBounds() {
        EventObject missingFailedLogon =
            CreateEvent(
                4625,
                DateTime.MinValue);
        var failed =
            new SecurityFailedLogonsReportBuilder(
                includeSamples: true,
                sampleSize: 1);
        var user =
            new SecurityUserLogonsReportBuilder(
                includeSamples: true,
                sampleSize: 1,
                eventIds: new[] { 4625 });

        failed.Add(missingFailedLogon);
        user.Add(missingFailedLogon);

        Assert.Null(failed.MinUtc);
        Assert.Null(failed.MaxUtc);
        Assert.Null(
            failed.Build()
                .Samples[0]
                .TimeCreatedUtc);
        Assert.Null(user.MinUtc);
        Assert.Null(user.MaxUtc);
        Assert.Null(
            user.Build()
                .Samples[0]
                .TimeCreatedUtc);
    }

    private static EventObject CreateEvent(
        int id,
        DateTime timeCreated) {

        var metadata = new NativeEventMetadata(
            "EventViewerX.Tests",
            providerId: null,
            id,
            qualifiers: null,
            level: 4,
            task: null,
            opcode: null,
            keywords: null,
            timeCreated,
            recordId: id,
            activityId: null,
            relatedActivityId: null,
            processId: null,
            threadId: null,
            logName: "Security",
            machineName: Environment.MachineName,
            userId: null,
            version: null);
        return new EventObject(
            metadata,
            Environment.MachineName,
            "Security");
    }
}
