using System;
using System.Management.Automation;
using System.Threading;
using EventViewerX;

namespace PSEventViewer;

internal sealed class PowerShellWatcherEventArgs : EventArgs {
    internal PowerShellWatcherEventArgs(object eventObject) {
        EventObject = eventObject;
    }

    /// <summary>The detached event snapshot delivered to the PowerShell action.</summary>
    public object EventObject { get; }
}

internal sealed class PowerShellWatcherEventBridge {
    internal static ScriptBlock ActionScript { get; } = ScriptBlock.Create(
        "$Sender.BeginAction(); try { $EventArgs.EventObject | ForEach-Object -Process { & $Event.MessageData $_ } } finally { $Sender.CompleteAction() }");

    private Action? _cleanup;
    private int _activeActions;
    private int _pendingActions;
    private int _cleanupRequested;
    private int _cleanupScheduled;

    /// <summary>Raised when the native event-log callback publishes a detached event snapshot.</summary>
    public event EventHandler<PowerShellWatcherEventArgs>? EventReceived;

    internal void Publish(object eventObject) {
        EventHandler<PowerShellWatcherEventArgs>? handler =
            EventReceived;
        if (handler == null) {
            return;
        }

        Interlocked.Increment(ref _pendingActions);
        try {
            handler.Invoke(
                this,
                new PowerShellWatcherEventArgs(eventObject));
        } catch {
            Interlocked.Decrement(ref _pendingActions);
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
    public void BeginAction() {
        Interlocked.Increment(ref _activeActions);
    }

    /// <summary>
    /// Marks a callback complete and schedules subscriber cleanup after the
    /// action job has returned to the PowerShell event manager.
    /// </summary>
    public void CompleteAction() {
        Interlocked.Decrement(ref _activeActions);
        Interlocked.Decrement(ref _pendingActions);
        TryScheduleCleanup();
    }

    internal void RequestCleanup(bool synchronousWhenIdle = false) {
        Interlocked.Exchange(
            ref _cleanupRequested,
            1);
        TryScheduleCleanup(synchronousWhenIdle);
    }

    private void TryScheduleCleanup(
        bool synchronousWhenIdle = false) {

        if (Volatile.Read(ref _cleanupRequested) == 0 ||
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
