using System.Runtime.InteropServices;

namespace EventViewerX.Native;

/// <summary>
/// Keeps native input handles alive until a bounded operation has actually
/// left unmanaged code, including after the caller times out or cancels.
/// </summary>
internal sealed class SafeHandleOperationLease : IDisposable {
    private readonly List<SafeHandle> _handles = new();
    private int _disposed;

    private SafeHandleOperationLease(
        IEnumerable<SafeHandle?> handles) {

        try {
            foreach (SafeHandle? handle in handles) {
                if (handle == null) {
                    continue;
                }
                bool added = false;
                handle.DangerousAddRef(ref added);
                if (!added) {
                    throw new ObjectDisposedException(
                        handle.GetType().Name);
                }
                _handles.Add(handle);
            }
        } catch {
            Dispose();
            throw;
        }
    }

    internal static SafeHandleOperationLease Capture(
        params SafeHandle?[] handles) {

        return new SafeHandleOperationLease(
            handles ??
            throw new ArgumentNullException(
                nameof(handles)));
    }

    public void Dispose() {
        if (Interlocked.Exchange(
                ref _disposed,
                1) != 0) {
            return;
        }
        for (int index = _handles.Count - 1;
             index >= 0;
             index--) {
            _handles[index].DangerousRelease();
        }
        _handles.Clear();
    }
}
