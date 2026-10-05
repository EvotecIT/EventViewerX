using System;
using System.Management.Automation;
using System.Threading;
using EventViewerX;

namespace PSEventViewer;

internal sealed class PowerShellWatcherEventArgs : EventArgs {
    internal PowerShellWatcherEventArgs(object eventObject, long ticket) {
        EventObject = eventObject;
        Ticket = ticket;
    }

    /// <summary>The detached event snapshot delivered to the PowerShell action.</summary>
    public object EventObject { get; }
    /// <summary>Bounded delivery reservation acknowledged by the action.</summary>
    public long Ticket { get; }
}

internal sealed class PowerShellWatcherEventBridge {
    internal static ScriptBlock ActionScript { get; } = ScriptBlock.Create(
        "$Sender.BeginAction($EventArgs.Ticket); $evxSucceeded = $false; try { $EventArgs.EventObject | ForEach-Object -Process { & $Event.MessageData $_ } -ErrorVariable evxActionErrors; $evxSucceeded = $? -and -not $evxActionErrors } finally { $Sender.CompleteAction($EventArgs.Ticket, $evxSucceeded) }");

    internal PowerShellWatcherEventBridge() : this(1024, 0) { }

    internal PowerShellWatcherEventBridge(int capacity, int deliveryLimit) {
        Delivery = new EventWatcherDelivery(capacity, deliveryLimit);
    }

    internal EventWatcherDelivery Delivery { get; }

    private Action? _cleanup;
    private int _activeActions;
    private int _pendingActions;
    private int _cleanupRequested;
    private int _cleanupScheduled;

    /// <summary>Raised when the native event-log callback publishes a detached event snapshot.</summary>
    public event EventHandler<PowerShellWatcherEventArgs>? EventReceived;

    internal void Publish(object eventObject) {
        PublishCore(eventObject, (eventObject as EventObject)?.TimeCreated);
    }

    internal void PublishProjected(object eventObject, DateTime? sourceTimeUtc) {
        PublishCore(eventObject, sourceTimeUtc);
    }

    private void PublishCore(object eventObject, DateTime? sourceTimeUtc) {
        EventHandler<PowerShellWatcherEventArgs>? handler =
            EventReceived;
        if (handler == null) {
            return;
        }

        long ticket = Delivery.TryAccept(sourceTimeUtc);
        if (ticket == 0) { return; }
        Interlocked.Increment(ref _pendingActions);
        try {
            handler.Invoke(
                this,
                new PowerShellWatcherEventArgs(eventObject, ticket));
        } catch {
            Delivery.Acknowledge(ticket, succeeded: false);
            Interlocked.Decrement(ref _pendingActions);
            TryScheduleCleanup();
            throw;
        }
    }

    internal void AttachCleanup(Action cleanup) {
        Volatile.Write(
            ref _cleanup,
            cleanup ??
            throw new ArgumentNullException(nameof(cleanup)));
    }

    /// <summary>Marks a PowerShell callback as active.</summary>
    public void BeginAction(long ticket) {
        Delivery.Begin(ticket);
        Interlocked.Increment(ref _activeActions);
    }

    /// <summary>
    /// Marks a callback complete and schedules subscriber cleanup after the
    /// action job has returned to the PowerShell event manager.
    /// </summary>
    public void CompleteAction(long ticket, bool succeeded) {
        Delivery.Acknowledge(ticket, succeeded);
        Interlocked.Decrement(ref _activeActions);
        Interlocked.Decrement(ref _pendingActions);
        TryScheduleCleanup();
    }

    internal void RequestCleanup(bool synchronousWhenIdle = false) {
        Delivery.StopAccepting();
        Interlocked.Exchange(
            ref _cleanupRequested,
            1);
        TryScheduleCleanup(synchronousWhenIdle);
    }

    private void TryScheduleCleanup(
        bool synchronousWhenIdle = false) {

        if (Volatile.Read(ref _cleanupRequested) == 0 ||
            !Delivery.GetHealth().IsDrained ||
            Volatile.Read(ref _activeActions) != 0 ||
            Volatile.Read(ref _pendingActions) != 0 ||
            Volatile.Read(ref _cleanup) == null ||
            Interlocked.Exchange(
                ref _cleanupScheduled,
                1) != 0) {
            return;
        }

        if (synchronousWhenIdle) {
            Volatile.Read(ref _cleanup)?.Invoke();
            return;
        }

        _ = Task.Run(async () => {
            // The PowerShell event action still owns its subscriber until the
            // action script returns. Defer removal past that return boundary.
            await Task.Delay(25).ConfigureAwait(false);
            Volatile.Read(ref _cleanup)?.Invoke();
        });
    }
}
