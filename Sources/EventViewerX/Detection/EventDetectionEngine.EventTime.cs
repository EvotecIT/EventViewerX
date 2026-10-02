namespace EventViewerX;

public static partial class EventDetectionEngine {
    private sealed partial class Evaluator {
        private SortedSet<BufferedObservation>? _reorderQueue;
        private DateTime _latestEventTimeUtc = DateTime.MinValue;
        private DateTime _eventTimeWatermarkUtc = DateTime.MinValue;
        private long _reorderBytes;
        private long _arrivalSequence;

        internal IEnumerable<EventDetectionFinding> Process(EventObservation observation) {
            _observations++;
            if (_options.MaximumObservations > 0 && _observations > _options.MaximumObservations) {
                _findings.Clear();
                if (!_observationBoundReported) {
                    _observationBoundReported = true;
                    _findings.Add(CreateIncomplete(observation,
                        $"MaximumObservations limit of {_options.MaximumObservations} was reached."));
                }
                return _findings;
            }
            return _options.EventTimeOrdering == null || _options.EventTimeOrdering.ReorderWindow == TimeSpan.Zero
                ? ProcessOrdered(observation)
                : ProcessWithOrdering(observation, _options.EventTimeOrdering);
        }

        internal IEnumerable<EventDetectionFinding> Complete() => _reorderQueue == null
            ? Array.Empty<EventDetectionFinding>()
            : DrainReorderQueue(complete: true);

        private IEnumerable<EventDetectionFinding> ProcessWithOrdering(EventObservation observation,
            EventTimeOrderingOptions ordering) {
            _reorderQueue ??= new SortedSet<BufferedObservation>(Comparer<BufferedObservation>.Create(
                static (left, right) => {
                    int result = CompareObservations(left.Observation, right.Observation);
                    return result != 0 ? result : left.Sequence.CompareTo(right.Sequence);
                }));
            if (observation.EventTimeUtc > _latestEventTimeUtc) {
                _latestEventTimeUtc = observation.EventTimeUtc;
                _eventTimeWatermarkUtc = new DateTime(Math.Max(0,
                    _latestEventTimeUtc.Ticks - ordering.ReorderWindow.Ticks), DateTimeKind.Utc);
            }
            if (observation.EventTimeUtc < _eventTimeWatermarkUtc) {
                string diagnostic = $"Observation arrived before event-time watermark {_eventTimeWatermarkUtc:O}; " +
                    $"ReorderWindow={ordering.ReorderWindow}. It was not evaluated.";
                if (ordering.LateArrivalPolicy == EventLateArrivalPolicy.Throw) {
                    throw new InvalidDataException(diagnostic);
                }
                yield return CreateIncomplete(observation, diagnostic);
                yield break;
            }
            foreach (EventDetectionFinding finding in DrainReorderQueue(complete: false)) {
                yield return finding;
            }
            long bytes = EstimateObservationBytes(observation) + 64;
            if (_reorderQueue.Count >= ordering.MaximumBufferedObservations ||
                bytes > ordering.MaximumBufferedBytes - _reorderBytes) {
                yield return CreateIncomplete(observation,
                    "Event-time reorder buffer limit was reached; the observation was not evaluated. " +
                    $"MaximumBufferedObservations={ordering.MaximumBufferedObservations}; " +
                    $"MaximumBufferedBytes={ordering.MaximumBufferedBytes}.");
                yield break;
            }
            _reorderQueue.Add(new BufferedObservation(observation, ++_arrivalSequence, bytes));
            _reorderBytes += bytes;
        }

        private IEnumerable<EventDetectionFinding> DrainReorderQueue(bool complete) {
            while (_reorderQueue!.Count > 0) {
                BufferedObservation first = _reorderQueue.Min!;
                if (!complete && first.Observation.EventTimeUtc >= _eventTimeWatermarkUtc) {
                    yield break;
                }
                _reorderQueue.Remove(first);
                _reorderBytes -= first.Bytes;
                foreach (EventDetectionFinding finding in ProcessOrdered(first.Observation)) {
                    yield return finding;
                }
            }
        }

        private sealed class BufferedObservation {
            internal BufferedObservation(EventObservation observation, long sequence, long bytes) {
                Observation = observation;
                Sequence = sequence;
                Bytes = bytes;
            }
            internal EventObservation Observation { get; }
            internal long Sequence { get; }
            internal long Bytes { get; }
        }
    }
}