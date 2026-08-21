using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace EventViewerX.Native;

internal sealed class WindowsEventXmlRenderer : IDisposable {
    private readonly NativeEventBuffer _buffer = new();
    private byte[]? _utf8Buffer;

    internal string Render(IntPtr eventHandle) {
        int bufferUsed = RenderIntoBuffer(eventHandle);
        return bufferUsed <= sizeof(char)
            ? string.Empty
            : Marshal.PtrToStringUni(
                _buffer.Pointer,
                (bufferUsed / sizeof(char)) - 1) ?? string.Empty;
    }

    internal unsafe void Write(IntPtr eventHandle, Stream destination) {
        int bufferUsed = RenderIntoBuffer(eventHandle);
        int characterCount = Math.Max(0, (bufferUsed / sizeof(char)) - 1);
        if (characterCount == 0) {
            return;
        }

        int maximumBytes = Encoding.UTF8.GetMaxByteCount(characterCount);
        EnsureUtf8Capacity(maximumBytes);
        int written;
        fixed (byte* output = _utf8Buffer!) {
            written = Encoding.UTF8.GetBytes(
                (char*)_buffer.Pointer,
                characterCount,
                output,
                _utf8Buffer!.Length);
        }
        destination.Write(_utf8Buffer!, 0, written);
    }

    private int RenderIntoBuffer(IntPtr eventHandle) {
        if (!WindowsEventNativeMethods.EvtRenderRaw(
                IntPtr.Zero,
                eventHandle,
                WindowsEventNativeMethods.RenderFlags.EventXml,
                _buffer.Capacity,
                _buffer.Pointer,
                out int bufferUsed,
                out _)) {

            int error = Marshal.GetLastWin32Error();
            if (error != WindowsEventNativeMethods.ErrorInsufficientBuffer) {
                throw new Win32Exception(error, "Failed to render Windows event XML.");
            }

            _buffer.EnsureCapacity(bufferUsed);
            if (!WindowsEventNativeMethods.EvtRenderRaw(
                    IntPtr.Zero,
                    eventHandle,
                    WindowsEventNativeMethods.RenderFlags.EventXml,
                    _buffer.Capacity,
                    _buffer.Pointer,
                    out bufferUsed,
                    out _)) {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to render Windows event XML.");
            }
        }

        return bufferUsed;
    }

    private void EnsureUtf8Capacity(int required) {
        if (_utf8Buffer != null && _utf8Buffer.Length >= required) {
            return;
        }
        if (_utf8Buffer != null) {
            ArrayPool<byte>.Shared.Return(_utf8Buffer);
        }
        _utf8Buffer = ArrayPool<byte>.Shared.Rent(required);
    }

    public void Dispose() {
        if (_utf8Buffer != null) {
            ArrayPool<byte>.Shared.Return(_utf8Buffer);
            _utf8Buffer = null;
        }
        _buffer.Dispose();
    }
}
