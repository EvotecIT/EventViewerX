using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Owns a bounded native Windows Event Log subscription.
/// Windows signals record availability; an owned worker pulls and projects
/// records outside the native notification path and applies real backpressure.
/// </summary>
public sealed class EventLogSubscription : IDisposable {
    private const int NativeBatchSize = 64;
    private readonly EventLogSubscriptionQuery _query;
    private readonly Action<EventObject> _eventHandler;
    private readonly Action<EventLogSubscriptionFailure>? _failureHandler;
    private readonly Channel<EventObject> _events;
    private readonly ManualResetEvent _signal = new(initialState: false);
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationToken _stopToken;
    private readonly AsyncLocal<int> _callbackDepth = new();
    private readonly WindowsEventNativeMethods.EventHandle? _session;
    private readonly WindowsEventNativeMethods.EventHandle? _bookmark;
    private readonly WindowsEventSnapshotProjector? _projector;
    private readonly WindowsEventNativeMethods.EventHandle? _subscription;
    private readonly bool _structuredQuery;
    private CancellationTokenRegistration _externalCancellation;
    private Task _producer = Task.CompletedTask;
    private Task _consumer = Task.CompletedTask;
    private int _stopping;
    private int _disposed;
    private int _resourcesDisposed;
    private long _eventsDelivered;

    /// <summary>Starts a native subscription.</summary>
    public EventLogSubscription(
        EventLogSubscriptionQuery query,
        Action<EventObject> eventHandler,
        Action<EventLogSubscriptionFailure>? failureHandler = null,
        CancellationToken cancellationToken = default) {

        _query = SnapshotAndValidate(query);
        cancellationToken.ThrowIfCancellationRequested();
        _eventHandler = eventHandler ??
            throw new ArgumentNullException(nameof(eventHandler));
        _failureHandler = failureHandler;
        _stopToken = _stop.Token;
        _events = Channel.CreateBounded<EventObject>(
            new BoundedChannelOptions(_query.BufferCapacity) {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

        try {
            string machineName =
                _query.MachineName?.Trim() ?? string.Empty;
            bool remote =
                !EventLogTarget.IsLocalMachine(machineName);
            if (remote) {
                _session = WindowsEventRemoteSession.OpenBounded(
                    machineName,
                    _query.Credential,
                    _query.Authentication,
                    _query.RemoteConnectionTimeoutMilliseconds,
                    cancellationToken);
            }
            if (_query.Start ==
                EventLogSubscriptionStart.AfterBookmark) {
                _bookmark =
                    WindowsEventNativeMethods.EvtCreateBookmark(
                        _query.BookmarkXml);
                if (_bookmark.IsInvalid) {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Failed to open the subscription bookmark.");
                }
            }

            _structuredQuery =
                EventLogStructuredQueryParser.IsQueryList(
                    _query.XPath);
            _projector = new WindowsEventSnapshotProjector(
                _query.ReadMode,
                _session?.DangerousGetHandle() ?? IntPtr.Zero,
                string.IsNullOrWhiteSpace(machineName)
                    ? Environment.MachineName
                    : machineName,
                _structuredQuery
                    ? string.Empty
                    : _query.LogName,
                _query.MessageCulture?.LCID ?? 0,
                _query.FallbackMessageCulture?.LCID ?? 0);
            SafeHandleOperationLease operationLease =
                SafeHandleOperationLease.Capture(
                    _session,
                    _signal.SafeWaitHandle,
                    _bookmark);
            IntPtr sessionHandle =
                _session?.DangerousGetHandle() ??
                IntPtr.Zero;
            IntPtr signalHandle =
                _signal.SafeWaitHandle
                    .DangerousGetHandle();
            IntPtr bookmarkHandle =
                _bookmark?.DangerousGetHandle() ??
                IntPtr.Zero;
            _subscription = CreateSubscriptionBounded(
                () => {
                    WindowsEventNativeMethods.EventHandle
                        subscription =
                            WindowsEventNativeMethods
                                .EvtSubscribe(
                                    sessionHandle,
                                    signalHandle,
                                    _structuredQuery
                                        ? null
                                        : _query.LogName,
                                    string.IsNullOrWhiteSpace(
                                        _query.XPath)
                                        ? "*"
                                        : _query.XPath,
                                    bookmarkHandle,
                                    IntPtr.Zero,
                                    callback: null,
                                    GetSubscribeFlags(
                                        _query));
                    if (!subscription.IsInvalid) {
                        return subscription;
                    }
                    int error =
                        Marshal.GetLastWin32Error();
                    subscription.Dispose();
                    throw new Win32Exception(
                        error,
                        $"Failed to subscribe to Windows event channel '{_query.LogName}'.");
                },
                _query.RemoteConnectionTimeoutMilliseconds,
                cancellationToken,
                operationLease);

            ReportInitialQueryFailuresBounded(
                cancellationToken);
            _producer = Task.Run(ProduceAsync);
            _consumer = Task.Run(ConsumeAsync);
            _externalCancellation =
                cancellationToken.CanBeCanceled
                    ? cancellationToken.Register(
                        static state =>
                            ((EventLogSubscription)state!)
                                .RequestExternalStop(),
                        this)
                    : default;
            try {
                cancellationToken
                    .ThrowIfCancellationRequested();
            } catch {
                _externalCancellation.Dispose();
                throw;
            }
        } catch {
            RequestStop();
            _externalCancellation.Dispose();
            WaitAndReleaseResources();
            throw;
        }
    }

    internal static WindowsEventNativeMethods.EventHandle
        CreateSubscriptionBounded(
            Func<WindowsEventNativeMethods.EventHandle> subscribe,
            int timeoutMilliseconds,
            CancellationToken cancellationToken,
            IDisposable? operationLease = null) {

        if (subscribe == null) {
            operationLease?.Dispose();
            throw new ArgumentNullException(
                nameof(subscribe));
        }
        return BoundedNativeOperation.Execute(
            subscribe,
            timeoutMilliseconds,
            $"Timed out starting the Windows event subscription after {timeoutMilliseconds} ms.",
            cancellationToken,
            static lateSubscription =>
                lateSubscription.Dispose(),
            operationLease);
    }

    /// <summary>Number of events successfully delivered to the consumer.</summary>
    public long EventsDelivered =>
        Interlocked.Read(ref _eventsDelivered);

    /// <summary>Whether the subscription is stopping or disposed.</summary>
    public bool IsStopped =>
        Volatile.Read(ref _stopping) != 0;

    private async Task ProduceAsync() {
        try {
            WaitHandle[] waits = {
                _signal,
                _stopToken.WaitHandle
            };
            // Prime the pull subscription once. Windows can make historical
            // records available before the caller begins waiting on the
            // notification event.
            await DrainAvailableEventsAsync()
                .ConfigureAwait(false);
            while (!_stopToken.IsCancellationRequested) {
                int signaled = WaitHandle.WaitAny(waits);
                if (signaled != 0 ||
                    _stopToken.IsCancellationRequested) {
                    break;
                }
                _signal.Reset();
                await DrainAvailableEventsAsync()
                    .ConfigureAwait(false);
            }
        } catch (OperationCanceledException)
            when (_stopToken.IsCancellationRequested) {
            // Normal shutdown.
        } catch (Exception exception) {
            ReportFailure(exception, terminal: true);
            RequestStop();
            _ = Task.Run(Dispose);
        } finally {
            _events.Writer.TryComplete();
        }
    }

    private async Task DrainAvailableEventsAsync() {
        var handles = new IntPtr[NativeBatchSize];
        try {
            while (!_stopToken.IsCancellationRequested) {
                Array.Clear(handles, 0, handles.Length);
                if (!WindowsEventNativeMethods.EvtNext(
                        _subscription!,
                        handles.Length,
                        handles,
                        timeout: 0,
                        flags: 0,
                        out int returned)) {
                    int error = Marshal.GetLastWin32Error();
                    if (error ==
                        WindowsEventNativeMethods.ErrorNoMoreItems) {
                        return;
                    }
                    if (error ==
                            WindowsEventNativeMethods.ErrorCancelled &&
                        _stopToken.IsCancellationRequested) {
                        return;
                    }
                    throw new Win32Exception(
                        error,
                        $"Failed while receiving subscription events from '{_query.LogName}'.");
                }

                for (int index = 0;
                     index < returned;
                     index++) {
                    IntPtr eventHandle = handles[index];
                    handles[index] = IntPtr.Zero;
                    try {
                        EventObject eventObject =
                            _projector!.Project(eventHandle);
                        await _events.Writer.WriteAsync(
                                eventObject,
                                _stopToken)
                            .ConfigureAwait(false);
                    } finally {
                        WindowsEventNativeMethods.EvtClose(
                            eventHandle);
                    }
                }
            }
        } finally {
            foreach (IntPtr handle in handles) {
                if (handle != IntPtr.Zero) {
                    WindowsEventNativeMethods.EvtClose(handle);
                }
            }
        }
    }

    private async Task ConsumeAsync() {
        try {
            while (await _events.Reader.WaitToReadAsync(
                       _stopToken).ConfigureAwait(false)) {
                while (_events.Reader.TryRead(
                           out EventObject? eventObject)) {
                    if (_stopToken.IsCancellationRequested) {
                        return;
                    }
                    try {
                        InvokeEventHandler(eventObject);
                        Interlocked.Increment(
                            ref _eventsDelivered);
                    } catch (Exception exception) {
                        ReportFailure(
                            exception,
                            terminal: false);
                    }
                }
            }
        } catch (OperationCanceledException)
            when (_stopToken.IsCancellationRequested) {
            // Normal shutdown.
        } finally {
            while (_events.Reader.TryRead(out _)) {
            }
        }
    }

    private void InvokeEventHandler(EventObject eventObject) {
        int previousDepth = _callbackDepth.Value;
        _callbackDepth.Value = previousDepth + 1;
        try {
            _eventHandler(eventObject);
        } finally {
            _callbackDepth.Value = previousDepth;
        }
    }

    private void ReportInitialQueryFailures() {
        if (!_query.TolerateQueryErrors) {
            return;
        }
        var nativeQuery = new NativeEventQuery(
            _session?.DangerousGetHandle() ?? IntPtr.Zero,
            _structuredQuery
                ? null
                : _query.LogName,
            _query.XPath,
            (_structuredQuery
                ? 0
                : WindowsEventNativeMethods.QueryFlags.ChannelPath) |
            WindowsEventNativeMethods.QueryFlags
                .TolerateQueryErrors,
            _query.LogName,
            machineName: _query.MachineName,
            failureHandler: _failureHandler == null
                ? null
                : failure =>
                    ReportFailure(
                        failure.Exception,
                        terminal: false));
        WindowsEventQueryDiagnostics.ReportFailures(
            _subscription!,
            nativeQuery);
    }

    private void ReportInitialQueryFailuresBounded(
        CancellationToken cancellationToken) {

        if (!_query.TolerateQueryErrors) {
            return;
        }
        SafeHandleOperationLease operationLease =
            SafeHandleOperationLease.Capture(
                _session,
                _subscription);
        ReportInitialQueryFailuresBounded(
            ReportInitialQueryFailures,
            _query.RemoteConnectionTimeoutMilliseconds,
            cancellationToken,
            operationLease);
    }

    internal static void ReportInitialQueryFailuresBounded(
        Action reportFailures,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        IDisposable? operationLease = null) {

        if (reportFailures == null) {
            operationLease?.Dispose();
            throw new ArgumentNullException(
                nameof(reportFailures));
        }
        _ = BoundedNativeOperation.Execute(
            () => {
                reportFailures();
                return true;
            },
            timeoutMilliseconds,
            $"Timed out reading initial subscription query diagnostics after {timeoutMilliseconds} ms.",
            cancellationToken,
            operationLease:
                operationLease);
    }

    private void ReportFailure(
        Exception exception,
        bool terminal) {

        try {
            if (_failureHandler == null) {
                return;
            }
            int previousDepth = _callbackDepth.Value;
            _callbackDepth.Value = previousDepth + 1;
            try {
                _failureHandler(
                    new EventLogSubscriptionFailure(
                        _query.LogName,
                        _query.MachineName,
                        exception,
                        terminal));
            } finally {
                _callbackDepth.Value = previousDepth;
            }
        } catch {
            // Failure reporting cannot unwind producer or consumer ownership.
        }
    }

    private void RequestStop() {
        if (Interlocked.Exchange(ref _stopping, 1) != 0) {
            return;
        }
        try {
            _stop.Cancel();
        } catch (ObjectDisposedException) {
            // A concurrent teardown already released the cancellation source.
        }
        try {
            _signal.Set();
        } catch (ObjectDisposedException) {
            // A concurrent teardown already released the wake-up handle.
        }
        try {
            if (_subscription != null &&
                !_subscription.IsInvalid &&
                !_subscription.IsClosed) {
                WindowsEventNativeMethods.EvtCancel(
                    _subscription);
            }
        } catch (ObjectDisposedException) {
            // Teardown can close the safe handle between the state check and EvtCancel.
        }
    }

    private void RequestExternalStop() {
        _ = Task.Run(() => {
            RequestStop();
            _externalCancellation.Dispose();
            WaitAndReleaseResources();
        });
    }

    /// <summary>Stops delivery and releases native handles.</summary>
    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        RequestStop();
        _externalCancellation.Dispose();
        if (_callbackDepth.Value > 0) {
            _ = Task.Run(WaitAndReleaseResources);
            return;
        }
        WaitAndReleaseResources();
    }

    private void WaitAndReleaseResources() {
        ExceptionDispatchInfo? failure = null;
        try {
            _producer.GetAwaiter().GetResult();
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            failure = ExceptionDispatchInfo.Capture(ex);
        }
        try {
            _consumer.GetAwaiter().GetResult();
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            if (failure == null) {
                failure = ExceptionDispatchInfo.Capture(ex);
            } else {
                failure.SourceException.Data["EventViewerX.SecondaryConsumerFailure"] = ex;
            }
        } finally {
            ReleaseSetupResources();
        }
        failure?.Throw();
    }

    private void ReleaseSetupResources() {
        if (Interlocked.Exchange(
                ref _resourcesDisposed,
                1) != 0) {
            return;
        }
        _subscription?.Dispose();
        _projector?.Dispose();
        _bookmark?.Dispose();
        _session?.Dispose();
        _signal.Dispose();
        _stop.Dispose();
    }

    private static EventLogSubscriptionQuery SnapshotAndValidate(
        EventLogSubscriptionQuery query) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventReadModeValidation.EnsureDefined(
            query.ReadMode,
            nameof(query));
        if (!Enum.IsDefined(
                typeof(EventLogSubscriptionStart),
                query.Start)) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The subscription starting position is not supported.");
        }
        if (query.Start ==
                EventLogSubscriptionStart.AfterBookmark &&
            string.IsNullOrWhiteSpace(query.BookmarkXml)) {
            throw new ArgumentException(
                "AfterBookmark requires BookmarkXml.",
                nameof(query));
        }
        if (query.Start !=
                EventLogSubscriptionStart.AfterBookmark &&
            !string.IsNullOrWhiteSpace(query.BookmarkXml)) {
            throw new ArgumentException(
                "BookmarkXml requires Start=AfterBookmark.",
                nameof(query));
        }
        if (query.BufferCapacity <= 0 ||
            query.BufferCapacity > 65536) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Subscription buffer capacity must be between 1 and 65536.");
        }
        if (query.RemoteConnectionTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Remote connection timeout must be greater than zero.");
        }
        if (!Enum.IsDefined(
                typeof(EventLogAuthentication),
                query.Authentication)) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The remote authentication value is not supported.");
        }
        string machineName =
            query.MachineName?.Trim() ?? string.Empty;
        if (EventLogTarget.IsLocalMachine(machineName) &&
            query.Credential != null) {
            throw new ArgumentException(
                "Credentials can only be used with a remote subscription.",
                nameof(query));
        }

        return new EventLogSubscriptionQuery(
            query.LogName) {
            MachineName =
                string.IsNullOrWhiteSpace(machineName)
                    ? null
                    : machineName,
            Credential = query.Credential,
            Authentication = query.Authentication,
            XPath =
                string.IsNullOrWhiteSpace(query.XPath)
                    ? "*"
                    : query.XPath,
            Start = query.Start,
            BookmarkXml = query.BookmarkXml,
            StrictBookmark = query.StrictBookmark,
            TolerateQueryErrors =
                query.TolerateQueryErrors,
            ReadMode = query.ReadMode,
            MessageCulture = query.MessageCulture,
            FallbackMessageCulture =
                query.FallbackMessageCulture,
            BufferCapacity = query.BufferCapacity,
            RemoteConnectionTimeoutMilliseconds =
                query.RemoteConnectionTimeoutMilliseconds
        };
    }

    private static WindowsEventNativeMethods.SubscribeFlags
        GetSubscribeFlags(
            EventLogSubscriptionQuery query) {

        WindowsEventNativeMethods.SubscribeFlags flags =
            query.Start switch {
                EventLogSubscriptionStart.Future =>
                    WindowsEventNativeMethods.SubscribeFlags
                        .ToFutureEvents,
                EventLogSubscriptionStart.Oldest =>
                    WindowsEventNativeMethods.SubscribeFlags
                        .StartAtOldestRecord,
                EventLogSubscriptionStart.AfterBookmark =>
                    WindowsEventNativeMethods.SubscribeFlags
                        .StartAfterBookmark,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(query))
            };
        if (query.TolerateQueryErrors) {
            flags |=
                WindowsEventNativeMethods.SubscribeFlags
                    .TolerateQueryErrors;
        }
        if (query.Start ==
                EventLogSubscriptionStart.AfterBookmark &&
            query.StrictBookmark) {
            flags |=
                WindowsEventNativeMethods.SubscribeFlags.Strict;
        }
        return flags;
    }
}
