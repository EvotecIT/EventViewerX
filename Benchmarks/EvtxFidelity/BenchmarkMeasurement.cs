using System.Diagnostics;
using EventViewerX;

internal static class MeasurementRunner {
    internal static Measurement Measure(Func<EventObject[]> action) {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        EventObject[] events = action();
        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        return new Measurement(
            events,
            new MeasurementSummary(
                events.Length,
                stopwatch.Elapsed.TotalMilliseconds,
                allocated,
                stopwatch.Elapsed.TotalSeconds == 0 ? 0 : events.Length / stopwatch.Elapsed.TotalSeconds,
                events.Length == 0 ? 0 : (double)allocated / events.Length));
    }
}

internal sealed record Measurement(EventObject[] Events, MeasurementSummary Summary);

internal sealed record MeasurementSummary(
    int Count,
    double ElapsedMilliseconds,
    long AllocatedBytes,
    double EventsPerSecond,
    double BytesPerEvent);

internal sealed record MeasurementAggregate(
    IReadOnlyList<MeasurementSummary> Iterations,
    MeasurementSummary Median) {

    internal static MeasurementAggregate Create(IReadOnlyList<MeasurementSummary> measurements) {
        if (measurements.Count == 0) {
            throw new ArgumentException("At least one measurement is required.", nameof(measurements));
        }
        return new MeasurementAggregate(
            measurements,
            new MeasurementSummary(
                CalculateMedian(measurements.Select(static item => item.Count)),
                CalculateMedian(measurements.Select(static item => item.ElapsedMilliseconds)),
                CalculateMedian(measurements.Select(static item => item.AllocatedBytes)),
                CalculateMedian(measurements.Select(static item => item.EventsPerSecond)),
                CalculateMedian(measurements.Select(static item => item.BytesPerEvent))));
    }

    private static int CalculateMedian(IEnumerable<int> values) =>
        checked((int)CalculateMedian(values.Select(static value => (double)value)));

    private static long CalculateMedian(IEnumerable<long> values) =>
        checked((long)CalculateMedian(values.Select(static value => (double)value)));

    private static double CalculateMedian(IEnumerable<double> values) {
        double[] ordered = values.OrderBy(static value => value).ToArray();
        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2;
    }
}

internal sealed record PerformanceBudgetResult(
    bool Configured,
    bool Passed,
    double? MinimumEventsPerSecond,
    double? MaximumBytesPerEvent,
    IReadOnlyList<string> Failures);

internal static class PerformanceBudgetEvaluator {
    internal static PerformanceBudgetResult Evaluate(
        BenchmarkOptions options,
        MeasurementSummary portable,
        FidelityAggregate? fidelity) {

        var failures = new List<string>();
        if (options.MinimumEventsPerSecond.HasValue &&
            portable.EventsPerSecond < options.MinimumEventsPerSecond.Value) {
            failures.Add(
                $"Portable throughput {portable.EventsPerSecond:F2} events/s is below the " +
                $"{options.MinimumEventsPerSecond.Value:F2} events/s budget.");
        }
        if (options.MaximumBytesPerEvent.HasValue &&
            portable.BytesPerEvent > options.MaximumBytesPerEvent.Value) {
            failures.Add(
                $"Portable allocation {portable.BytesPerEvent:F2} bytes/event exceeds the " +
                $"{options.MaximumBytesPerEvent.Value:F2} bytes/event budget.");
        }
        if (fidelity != null && fidelity.MinimumIdentityMatchRatio < options.MinimumIdentityMatchRatio) {
            failures.Add(
                $"Portable identity ratio {fidelity.MinimumIdentityMatchRatio:P2} is below the " +
                $"{options.MinimumIdentityMatchRatio:P2} gate.");
        }

        bool configured = options.MinimumEventsPerSecond.HasValue || options.MaximumBytesPerEvent.HasValue;
        return new PerformanceBudgetResult(
            configured,
            failures.Count == 0,
            options.MinimumEventsPerSecond,
            options.MaximumBytesPerEvent,
            failures);
    }
}
