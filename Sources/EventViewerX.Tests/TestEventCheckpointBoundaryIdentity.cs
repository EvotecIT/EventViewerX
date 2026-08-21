using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventCheckpointBoundaryIdentity {
    [Fact]
    public void SourceNameCasingDoesNotChangeBoundaryIdentity() {
        EventObject first = CreateEvent(
            "DC01.AD.EXAMPLE",
            "SECURITY");
        EventObject second = CreateEvent(
            "dc01.ad.example",
            "security");

        Assert.Equal(
            EventCheckpointBoundaryIdentity.Create(first),
            EventCheckpointBoundaryIdentity.Create(second));
    }

    private static EventObject CreateEvent(
        string machineName,
        string containerLog) {

        var metadata = new NativeEventMetadata(
            "Microsoft-Windows-Security-Auditing",
            Guid.Parse(
                "54849625-5478-4994-A5BA-3E3B0328C30D"),
            id: 4624,
            qualifiers: null,
            level: 0,
            task: 12544,
            opcode: 0,
            keywords: unchecked((long)0x8020000000000000),
            timeCreated: new DateTime(
                2026,
                7,
                25,
                1,
                2,
                3,
                DateTimeKind.Utc),
            recordId: 1234,
            activityId: null,
            relatedActivityId: null,
            processId: 100,
            threadId: 200,
            logName: containerLog,
            machineName,
            userId: null,
            version: 2);
        return new EventObject(
            metadata,
            queriedMachine: machineName,
            containerLog);
    }
}
