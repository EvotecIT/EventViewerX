using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace EventViewerX.Native;

internal sealed class WindowsEventHandleEnumerator : IDisposable {
    private const int BatchSize = 64;
    private readonly CancellationToken _cancellationToken;
    private readonly NativeEventQuery _eventQuery;
    private readonly IntPtr[] _handles = new IntPtr[BatchSize];
    private readonly WindowsEventNativeMethods.EventHandle _query;
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private int _index;
    private int _returned;
    private bool _disposed;

    internal WindowsEventHandleEnumerator(
        NativeEventQuery eventQuery,
        CancellationToken cancellationToken) {

        _eventQuery = eventQuery;
        _cancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        _query = WindowsEventNativeMethods.EvtQuery(
            eventQuery.Session,
            eventQuery.Path,
            eventQuery.XPath,
            eventQuery.Flags);
        if (_query.IsInvalid) {
            int error = Marshal.GetLastWin32Error();
            _query.Dispose();
            throw new Win32Exception(
                error,
                $"Failed to query Windows event source '{eventQuery.DisplayName}'.");
        }
        _cancellationRegistration = cancellationToken.Register(
            static state =>
                ((WindowsEventHandleEnumerator)state!)
                    .CancelPendingRead(),
            this);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            WindowsEventQueryDiagnostics.ReportFailures(_query, eventQuery);
            cancellationToken.ThrowIfCancellationRequested();
            SeekToBookmark();
            cancellationToken.ThrowIfCancellationRequested();
        } catch {
            _cancellationRegistration.Dispose();
            _query.Dispose();
            throw;
        }
    }

    internal IntPtr Current { get; private set; }

    internal void ReleaseCurrent() {
        CloseCurrent();
    }

    internal bool MoveNext() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(WindowsEventHandleEnumerator));
        }
        _cancellationToken.ThrowIfCancellationRequested();
        CloseCurrent();

        if (_index >= _returned && !ReadBatch()) {
            return false;
        }

        Current = _handles[_index];
        _handles[_index] = IntPtr.Zero;
        _index++;
        return true;
    }

    private bool ReadBatch() {
        Array.Clear(_handles, 0, _handles.Length);
        _index = 0;
        _returned = 0;
        if (WindowsEventNativeMethods.EvtNext(
                _query,
                _handles.Length,
                _handles,
                _eventQuery.NextTimeoutMilliseconds > 0
                    ? _eventQuery.NextTimeoutMilliseconds
                    : -1,
                0,
                out _returned)) {
            return _returned > 0;
        }

        int error = Marshal.GetLastWin32Error();
        if (error == WindowsEventNativeMethods.ErrorCancelled &&
            _cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException(_cancellationToken);
        }
        if (error == WindowsEventNativeMethods.ErrorNoMoreItems) {
            return false;
        }
        if (error == WindowsEventNativeMethods.ErrorTimeout) {
            throw new TimeoutException(
                $"Timed out while reading Windows event source '{_eventQuery.DisplayName}'.");
        }
        throw new Win32Exception(
            error,
            $"Failed while reading Windows event source '{_eventQuery.DisplayName}'.");
    }

    private void SeekToBookmark() {
        if (string.IsNullOrWhiteSpace(_eventQuery.BookmarkXml)) {
            return;
        }

        using WindowsEventNativeMethods.EventHandle bookmark =
            WindowsEventNativeMethods.EvtCreateBookmark(_eventQuery.BookmarkXml);
        if (bookmark.IsInvalid) {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                error,
                $"Failed to open the bookmark for Windows event source '{_eventQuery.DisplayName}'.");
        }

        WindowsEventNativeMethods.SeekFlags flags =
            WindowsEventNativeMethods.SeekFlags.RelativeToBookmark;
        if (_eventQuery.StrictBookmark) {
            flags |= WindowsEventNativeMethods.SeekFlags.Strict;
        }
        if (!WindowsEventNativeMethods.EvtSeek(
                _query,
                _eventQuery.BookmarkOffset,
                bookmark,
                0,
                flags)) {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                error,
                $"Failed to seek from the bookmark for Windows event source '{_eventQuery.DisplayName}'.");
        }
    }

    private void CancelPendingRead() {
        if (!_query.IsClosed && !_query.IsInvalid) {
            WindowsEventNativeMethods.EvtCancel(_query);
        }
    }

    private void CloseCurrent() {
        if (Current != IntPtr.Zero) {
            WindowsEventNativeMethods.EvtClose(Current);
            Current = IntPtr.Zero;
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _cancellationRegistration.Dispose();
        CloseCurrent();
        for (int index = _index; index < _returned; index++) {
            if (_handles[index] != IntPtr.Zero) {
                WindowsEventNativeMethods.EvtClose(_handles[index]);
                _handles[index] = IntPtr.Zero;
            }
        }
        _query.Dispose();
    }
}
