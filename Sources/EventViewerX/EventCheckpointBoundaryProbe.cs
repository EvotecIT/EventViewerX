namespace EventViewerX;

internal enum EventCheckpointBoundaryProbeState {
    Found,
    ExhaustedWithoutMatch,
    CheckpointAboveObservedHead,
    LimitReached
}

internal readonly struct EventCheckpointBoundaryProbeResult {
    internal EventCheckpointBoundaryProbeResult(
        EventCheckpointBoundaryProbeState state,
        EventObject? boundaryEvent) {

        State = state;
        BoundaryEvent = boundaryEvent;
    }

    internal EventCheckpointBoundaryProbeState State { get; }

    internal EventObject? BoundaryEvent { get; }
}

internal static class EventCheckpointBoundaryProbe {
    internal static EventCheckpointBoundaryProbeResult Find(
        IEnumerable<EventObject> events,
        long checkpoint,
        long maximumEvents) {

        if (events == null) {
            throw new ArgumentNullException(nameof(events));
        }
        if (maximumEvents <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumEvents));
        }

        long inspected = 0;
        long? highestObservedRecordId = null;
        foreach (EventObject eventObject in events) {
            if (inspected >= maximumEvents) {
                if (highestObservedRecordId.HasValue && checkpoint > highestObservedRecordId.Value) {
                    return new EventCheckpointBoundaryProbeResult(
                        EventCheckpointBoundaryProbeState.CheckpointAboveObservedHead,
                        boundaryEvent: null);
                }
                return new EventCheckpointBoundaryProbeResult(
                    EventCheckpointBoundaryProbeState.LimitReached,
                    boundaryEvent: null);
            }
            inspected++;
            if (eventObject.RecordId.HasValue &&
                (!highestObservedRecordId.HasValue || eventObject.RecordId.Value > highestObservedRecordId.Value)) {
                highestObservedRecordId = eventObject.RecordId.Value;
            }
            if (eventObject.RecordId == checkpoint) {
                return new EventCheckpointBoundaryProbeResult(
                    EventCheckpointBoundaryProbeState.Found,
                    eventObject);
            }
        }

        return new EventCheckpointBoundaryProbeResult(
            EventCheckpointBoundaryProbeState.ExhaustedWithoutMatch,
            boundaryEvent: null);
    }
}
