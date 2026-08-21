namespace EventViewerX;

/// <summary>
/// Removes duplicate delivery of one record when a logical watcher is backed
/// by several overlapping native query partitions.
/// </summary>
internal sealed class EventDeliveryDeduplicator {
    private const int MaximumRememberedEvents = 65536;
    private readonly object _sync = new();
    private readonly HashSet<EventDeliveryIdentity> _seen = new();
    private readonly Queue<EventDeliveryIdentity> _order = new();

    internal bool TryAccept(
        EventObject eventObject) {

        if (eventObject == null) {
            throw new ArgumentNullException(
                nameof(eventObject));
        }
        EventDeliveryIdentity identity =
            EventDeliveryIdentity.Create(
                eventObject);
        lock (_sync) {
            if (!_seen.Add(identity)) {
                return false;
            }
            _order.Enqueue(identity);
            while (_order.Count >
                   MaximumRememberedEvents) {
                _seen.Remove(
                    _order.Dequeue());
            }
            return true;
        }
    }

    private readonly struct EventDeliveryIdentity :
        IEquatable<EventDeliveryIdentity> {
        private readonly string _machineName;
        private readonly string _containerLog;
        private readonly long? _recordId;
        private readonly string _fallbackIdentity;

        private EventDeliveryIdentity(
            string machineName,
            string containerLog,
            long? recordId,
            string fallbackIdentity) {

            _machineName = machineName;
            _containerLog = containerLog;
            _recordId = recordId;
            _fallbackIdentity = fallbackIdentity;
        }

        internal static EventDeliveryIdentity Create(
            EventObject eventObject) {

            string containerLog =
                string.IsNullOrWhiteSpace(
                    eventObject.ContainerLog)
                    ? eventObject.LogName
                    : eventObject.ContainerLog;
            return new EventDeliveryIdentity(
                eventObject.MachineName ?? string.Empty,
                containerLog ?? string.Empty,
                eventObject.RecordId,
                eventObject.RecordId.HasValue
                    ? string.Empty
                    : EventCheckpointBoundaryIdentity
                        .Create(eventObject));
        }

        public bool Equals(
            EventDeliveryIdentity other) {

            return _recordId == other._recordId &&
                   string.Equals(
                       _machineName,
                       other._machineName,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       _containerLog,
                       other._containerLog,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       _fallbackIdentity,
                       other._fallbackIdentity,
                       StringComparison.Ordinal);
        }

        public override bool Equals(
            object? obj) {

            return obj is EventDeliveryIdentity other &&
                   Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                int hash =
                    StringComparer.OrdinalIgnoreCase
                        .GetHashCode(
                            _machineName);
                hash = (hash * 397) ^
                       StringComparer.OrdinalIgnoreCase
                           .GetHashCode(
                               _containerLog);
                hash = (hash * 397) ^
                       _recordId.GetHashCode();
                hash = (hash * 397) ^
                       StringComparer.Ordinal
                           .GetHashCode(
                               _fallbackIdentity);
                return hash;
            }
        }
    }
}
