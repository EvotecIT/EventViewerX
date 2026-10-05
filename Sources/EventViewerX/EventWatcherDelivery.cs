namespace EventViewerX;

/// <summary>Detached snapshot of accepted action work. Completion and acknowledgement are separate from stopping collection.</summary>
public sealed class EventWatcherHealth {
    internal EventWatcherHealth(long queued, long running, long completed, long failed, long acknowledged,
        long rejected, TimeSpan? oldestPendingAge, TimeSpan? sourceLag, bool stopping) {
        Queued = queued; Running = running; Completed = completed; Failed = failed;
        Acknowledged = acknowledged; Rejected = rejected; OldestPendingAge = oldestPendingAge;
        SourceLag = sourceLag; IsStopping = stopping;
    }
    /// <summary>Accepted actions waiting to begin.</summary>
    public long Queued { get; }
    /// <summary>Actions currently executing.</summary>
    public long Running { get; }
    /// <summary>Actions that returned successfully.</summary>
    public long Completed { get; }
    /// <summary>Actions that failed.</summary>
    public long Failed { get; }
    /// <summary>Actions whose completion was acknowledged by the host.</summary>
    public long Acknowledged { get; }
    /// <summary>Deliveries rejected because the bounded queue was full.</summary>
    public long Rejected { get; }
    /// <summary>Age of the oldest accepted, unacknowledged action.</summary>
    public TimeSpan? OldestPendingAge { get; }
    /// <summary>Receive time minus raw source time; this is not a measurement of clock skew.</summary>
    public TimeSpan? SourceLag { get; }
    /// <summary>Whether collection has been asked to stop.</summary>
    public bool IsStopping { get; }
    /// <summary>Whether overload has occurred. This remains true after draining.</summary>
    public bool IsOverloaded => Rejected != 0;
    /// <summary>Whether every accepted action has been acknowledged.</summary>
    public bool IsDrained => Queued == 0 && Running == 0;
}

/// <summary>Bounded action admission and drain tracking shared by watcher hosts.</summary>
public sealed class EventWatcherDelivery {
    private readonly object _gate = new();
    private readonly Dictionary<long, PendingAction> _pending = new();
    private readonly LinkedList<DateTime> _acceptanceTimes = new();
    private readonly TaskCompletionSource<bool> _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int _capacity;
    private readonly int _limit;
    private long _accepted, _running, _completed, _failed, _acknowledged, _rejected;
    private bool _stopping;
    private TimeSpan? _sourceLag;

    /// <summary>Creates bounded tracking. Zero delivery limit means no lifetime limit.</summary>
    public EventWatcherDelivery(int capacity = 1024, int deliveryLimit = 0) {
        if (capacity <= 0) { throw new ArgumentOutOfRangeException(nameof(capacity)); }
        if (deliveryLimit < 0) { throw new ArgumentOutOfRangeException(nameof(deliveryLimit)); }
        _capacity = capacity;
        _limit = deliveryLimit;
    }

    /// <summary>Raised once when admission closes because of an explicit stop, limit, or overload. Handlers must not block.</summary>
    public event EventHandler? StopRequested;

    /// <summary>Atomically reserves an action before a host queues it. Returns zero when admission is closed.</summary>
    public long TryAccept(DateTime? sourceTimeUtc = null) {
        bool notify = false;
        long ticket = 0;
        lock (_gate) {
            if (_stopping) { return 0; }
            if (_pending.Count >= _capacity) {
                _rejected++;
                _stopping = notify = true;
            } else {
                ticket = ++_accepted;
                DateTime now = DateTime.UtcNow;
                _pending.Add(ticket, new PendingAction(_acceptanceTimes.AddLast(now)));
                if (sourceTimeUtc.HasValue) { _sourceLag = now - sourceTimeUtc.Value.ToUniversalTime(); }
                if (_limit > 0 && _accepted >= _limit) { _stopping = notify = true; }
            }
        }
        if (notify) {
            try { StopRequested?.Invoke(this, EventArgs.Empty); }
            catch { if (ticket != 0) { Acknowledge(ticket, succeeded: false); } throw; }
        }
        return ticket;
    }

    /// <summary>Marks an accepted action running. Duplicate notifications are ignored.</summary>
    public void Begin(long ticket) {
        lock (_gate) {
            if (_pending.TryGetValue(ticket, out PendingAction? action) && !action.Running) {
                action.Running = true;
                _running++;
            }
        }
    }

    /// <summary>Acknowledges completion, including failure to enqueue. Duplicate acknowledgements are ignored.</summary>
    public void Acknowledge(long ticket, bool succeeded) {
        lock (_gate) {
            if (!_pending.TryGetValue(ticket, out PendingAction? action)) { return; }
            _pending.Remove(ticket);
            _acceptanceTimes.Remove(action.Accepted);
            if (action.Running) { _running--; }
            if (succeeded) { _completed++; } else { _failed++; }
            _acknowledged++;
            CheckDrained();
        }
    }

    /// <summary>Closes admission while retaining all accepted work for completion.</summary>
    public void StopAccepting() {
        bool notify;
        lock (_gate) {
            notify = !_stopping;
            _stopping = true;
            CheckDrained();
        }
        if (notify) { StopRequested?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Completes after admission closes and every accepted action is acknowledged. It never cancels accepted actions.</summary>
    public Task DrainCompletion => _drained.Task;

    /// <summary>Returns a consistent health snapshot.</summary>
    public EventWatcherHealth GetHealth() {
        lock (_gate) {
            TimeSpan? age = _acceptanceTimes.First == null ? null : DateTime.UtcNow - _acceptanceTimes.First.Value;
            return new EventWatcherHealth(_pending.Count - _running, _running, _completed, _failed,
                _acknowledged, _rejected, age, _sourceLag, _stopping);
        }
    }

    private void CheckDrained() {
        if (_stopping && _pending.Count == 0) { _drained.TrySetResult(true); }
    }

    private sealed class PendingAction {
        internal PendingAction(LinkedListNode<DateTime> accepted) { Accepted = accepted; }
        internal LinkedListNode<DateTime> Accepted { get; }
        internal bool Running { get; set; }
    }
}
