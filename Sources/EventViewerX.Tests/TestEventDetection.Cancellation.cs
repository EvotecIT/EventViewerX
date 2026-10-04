using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaterializedEvaluationStopsWhenCancellationArrivesInsideRuleEvaluation(bool throwFromValue) {
        using var cancellation = new CancellationTokenSource();
        var value = new CancelDuringEvaluation(cancellation, throwFromValue);
        EventObservation[] observations = CreateCancellationObservations(value);

        OperationCanceledException error = Assert.ThrowsAny<OperationCanceledException>(() =>
            EventDetectionEngine.Evaluate(observations, CancellationPlan(), null, cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, value.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncEvaluationHonorsCancellationEvenWhenTheSourceDoesNot(bool throwFromValue) {
        using var cancellation = new CancellationTokenSource();
        var value = new CancelDuringEvaluation(cancellation, throwFromValue);
        EventObservation[] observations = CreateCancellationObservations(value);

        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await foreach (EventDetectionFinding _ in EventDetectionEngine.StreamAsync(
                UncooperativeSource(), CancellationPlan(), cancellationToken: cancellation.Token)) {
            }
        });

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, value.Reads);

        async IAsyncEnumerable<EventObservation> UncooperativeSource() {
            foreach (EventObservation item in observations) {
                await Task.Yield();
                yield return item;
            }
        }
    }

    private static EventObservation[] CreateCancellationObservations(CancelDuringEvaluation value) =>
        Enumerable.Range(0, 5).Select(index => {
            DateTime time = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc).AddSeconds(index);
            EventObject source = CreateEvent(1001, time, index + 1, "Security", "Provider-A");
            return EventObservation.Restore(source, "cancel-" + index, "Generic",
                new Dictionary<string, object?> { ["Account"] = value }, time, time);
        }).ToArray();

    private static EventDetectionPlan CancellationPlan() => EventDetectionPlan.Compile(new[] {
        new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "EVX-CANCELLATION", Title = "Cancellation during grouping",
            Kind = EventDetectionRuleKind.Threshold, EventIds = new[] { 1001 },
            Threshold = 3, GroupBy = "Account", Window = TimeSpan.FromMinutes(5)
        })
    });

    private sealed class CancelDuringEvaluation : IFormattable {
        private readonly CancellationTokenSource _source;
        private readonly bool _throwFromValue;
        internal CancelDuringEvaluation(CancellationTokenSource source, bool throwFromValue) {
            _source = source;
            _throwFromValue = throwFromValue;
        }
        internal int Reads { get; private set; }
        public string ToString(string? format, IFormatProvider? formatProvider) {
            Reads++;
            _source.Cancel();
            if (_throwFromValue) {
                _source.Token.ThrowIfCancellationRequested();
            }
            return "alice";
        }
    }
}
