using EventViewerX.Native;

namespace EventViewerX.Benchmarks;

/// <summary>Creates equivalent typed-event projection workloads for PowerForge benchmarks.</summary>
public static class EventProjectionBenchmarkFixture {
    /// <summary>Creates a repeated specialized-logon workload and its reusable projection plan.</summary>
    public static EventProjectionBenchmarkState Create(int eventCount) {
        if (eventCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(eventCount));
        }

        var metadata = new NativeEventMetadata(
            "Microsoft-Windows-Security-Auditing",
            Guid.Parse("54849625-5478-4994-A5BA-3E3B0328C30D"),
            id: 4624,
            qualifiers: null,
            level: 0,
            task: 12544,
            opcode: 0,
            keywords: unchecked((long)0x8020000000000000),
            timeCreated: new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc),
            recordId: 42,
            activityId: null,
            relatedActivityId: null,
            processId: 100,
            threadId: 200,
            logName: "Security",
            machineName: "server01",
            userId: null,
            version: 2);
        var source = new EventObject(metadata, queriedMachine: "server01", containerLog: "Security");
        source.Data["LmPackageName"] = "NTLM V1";
        source.Data["LogonType"] = "3";
        source.Data["TargetUserName"] = "alice";
        source.Data["TargetDomainName"] = "CONTOSO";

        EventType[] requestedTypes = { EventType.ActiveDirectoryAuthentication };
        EventTypeProjectionPlan plan = EventTypeCatalog.CompileProjectionPlan(requestedTypes);
        object projected = EventTypeCatalog.CreateEventRule(source, plan)
            ?? throw new InvalidOperationException("The benchmark fixture did not produce a typed event.");
        string projectedType = projected.GetType().FullName ?? projected.GetType().Name;
        int checksumPerEvent = CalculateChecksum(projectedType);
        return new EventProjectionBenchmarkState(
            source,
            requestedTypes,
            plan,
            eventCount,
            projectedType,
            unchecked(checksumPerEvent * eventCount));
    }

    /// <summary>Projects events through the compatibility overload that recompiles selection for each event.</summary>
    public static EventProjectionBenchmarkResult RunCompilePerEvent(EventProjectionBenchmarkState state) =>
        Run(state, static current => EventTypeCatalog.CreateEventRule(current.Source, current.RequestedTypes));

    /// <summary>Projects events through one immutable, precompiled selection plan.</summary>
    public static EventProjectionBenchmarkResult RunReusablePlan(EventProjectionBenchmarkState state) =>
        Run(state, static current => EventTypeCatalog.CreateEventRule(current.Source, current.Plan));

    private static EventProjectionBenchmarkResult Run(
        EventProjectionBenchmarkState state,
        Func<EventProjectionBenchmarkState, object?> project) {

        if (state == null) {
            throw new ArgumentNullException(nameof(state));
        }
        int projectedCount = 0;
        int checksum = 0;
        string? projectedType = null;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < state.EventCount; index++) {
            object? result = project(state);
            if (result == null) {
                continue;
            }
            projectedCount++;
            projectedType = result.GetType().FullName ?? result.GetType().Name;
            checksum = unchecked(checksum + CalculateChecksum(projectedType));
        }
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return new EventProjectionBenchmarkResult(
            projectedCount,
            checksum,
            projectedType ?? string.Empty,
            allocatedBytes);
    }

    private static int CalculateChecksum(string value) {
        int checksum = 17;
        foreach (char character in value) {
            checksum = unchecked(checksum * 31 + character);
        }
        return checksum;
    }
}

/// <summary>Immutable inputs and expected outputs for one event-projection benchmark case.</summary>
public sealed class EventProjectionBenchmarkState {
    internal EventProjectionBenchmarkState(
        EventObject source,
        EventType[] requestedTypes,
        EventTypeProjectionPlan plan,
        int eventCount,
        string expectedType,
        int expectedChecksum) {

        Source = source;
        RequestedTypes = requestedTypes;
        Plan = plan;
        EventCount = eventCount;
        ExpectedType = expectedType;
        ExpectedChecksum = expectedChecksum;
    }

    internal EventObject Source { get; }
    internal EventType[] RequestedTypes { get; }
    internal EventTypeProjectionPlan Plan { get; }

    /// <summary>Number of projections executed by each engine.</summary>
    public int EventCount { get; }

    /// <summary>Expected specialized CLR projection type.</summary>
    public string ExpectedType { get; }

    /// <summary>Expected deterministic checksum across all projections.</summary>
    public int ExpectedChecksum { get; }
}

/// <summary>Correctness evidence emitted by one projection benchmark engine.</summary>
public sealed class EventProjectionBenchmarkResult {
    internal EventProjectionBenchmarkResult(
        int projectedCount,
        int checksum,
        string projectedType,
        long allocatedBytes) {

        ProjectedCount = projectedCount;
        Checksum = checksum;
        ProjectedType = projectedType;
        AllocatedBytes = allocatedBytes;
    }

    /// <summary>Number of non-null typed projections.</summary>
    public int ProjectedCount { get; }

    /// <summary>Deterministic checksum of every projected CLR type.</summary>
    public int Checksum { get; }

    /// <summary>CLR type produced by the final projection.</summary>
    public string ProjectedType { get; }

    /// <summary>Managed bytes allocated on the executing thread while projecting the workload.</summary>
    public long AllocatedBytes { get; }
}
