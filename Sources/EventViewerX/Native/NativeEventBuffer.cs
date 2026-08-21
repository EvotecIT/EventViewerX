using System;
using System.Runtime.InteropServices;

namespace EventViewerX.Native;

internal sealed class NativeEventBuffer : IDisposable {
    private const int InitialCapacity = 2048;
    private IntPtr _buffer;

    internal NativeEventBuffer() {
        Capacity = InitialCapacity;
        _buffer = Marshal.AllocHGlobal(Capacity);
    }

    internal int Capacity { get; private set; }

    internal IntPtr Pointer => _buffer;

    internal void EnsureCapacity(int requiredCapacity) {
        if (requiredCapacity <= Capacity) {
            return;
        }

        _buffer = Marshal.ReAllocHGlobal(_buffer, new IntPtr(requiredCapacity));
        Capacity = requiredCapacity;
    }

    public void Dispose() {
        if (_buffer == IntPtr.Zero) {
            return;
        }

        Marshal.FreeHGlobal(_buffer);
        _buffer = IntPtr.Zero;
        Capacity = 0;
    }
}
