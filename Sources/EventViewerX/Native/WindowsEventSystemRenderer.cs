using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EventViewerX.Native;

internal sealed class WindowsEventSystemRenderer : IDisposable {
    private readonly NativeEventBuffer _buffer;
    private readonly WindowsEventNativeMethods.EventHandle _renderContext;

    internal WindowsEventSystemRenderer() {
        WindowsEventNativeMethods.EventHandle renderContext =
            WindowsEventNativeMethods.EvtCreateRenderContext(
            0,
            null,
            WindowsEventNativeMethods.RenderContextFlags.System);
        if (renderContext.IsInvalid) {
            int error = Marshal.GetLastWin32Error();
            renderContext.Dispose();
            throw new Win32Exception(
                error,
                "Failed to create the Windows event system render context.");
        }

        try {
            _buffer = new NativeEventBuffer();
            _renderContext = renderContext;
        } catch {
            renderContext.Dispose();
            throw;
        }
    }

    internal unsafe NativeEventMetadata Render(IntPtr eventHandle) {
        if (!WindowsEventNativeMethods.EvtRender(
                _renderContext,
                eventHandle,
                WindowsEventNativeMethods.RenderFlags.EventValues,
                _buffer.Capacity,
                _buffer.Pointer,
                out int bufferUsed,
                out int propertyCount)) {

            int error = Marshal.GetLastWin32Error();
            if (error != WindowsEventNativeMethods.ErrorInsufficientBuffer) {
                throw new Win32Exception(error, "Failed to render Windows event system properties.");
            }

            _buffer.EnsureCapacity(bufferUsed);
            if (!WindowsEventNativeMethods.EvtRender(
                    _renderContext,
                    eventHandle,
                    WindowsEventNativeMethods.RenderFlags.EventValues,
                    _buffer.Capacity,
                    _buffer.Pointer,
                    out bufferUsed,
                    out propertyCount)) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to render Windows event system properties.");
            }
        }

        if (propertyCount < WindowsEventNativeMethods.SystemPropertyCount) {
            throw new InvalidOperationException($"Windows returned {propertyCount} event system properties; expected {WindowsEventNativeMethods.SystemPropertyCount}.");
        }

        var variants =
            (WindowsEventNativeMethods.EventVariant*)_buffer.Pointer;
        WindowsEventNativeMethods.EventVariant providerName = variants[0];
        WindowsEventNativeMethods.EventVariant providerId = variants[1];
        WindowsEventNativeMethods.EventVariant eventId = variants[2];
        WindowsEventNativeMethods.EventVariant qualifiers = variants[3];
        WindowsEventNativeMethods.EventVariant level = variants[4];
        WindowsEventNativeMethods.EventVariant task = variants[5];
        WindowsEventNativeMethods.EventVariant opcode = variants[6];
        WindowsEventNativeMethods.EventVariant keywords = variants[7];
        WindowsEventNativeMethods.EventVariant timeCreated = variants[8];
        WindowsEventNativeMethods.EventVariant recordId = variants[9];
        WindowsEventNativeMethods.EventVariant activityId = variants[10];
        WindowsEventNativeMethods.EventVariant relatedActivityId =
            variants[11];
        WindowsEventNativeMethods.EventVariant processId = variants[12];
        WindowsEventNativeMethods.EventVariant threadId = variants[13];
        WindowsEventNativeMethods.EventVariant channel = variants[14];
        WindowsEventNativeMethods.EventVariant computer = variants[15];
        WindowsEventNativeMethods.EventVariant userId = variants[16];
        WindowsEventNativeMethods.EventVariant version = variants[17];

        return new NativeEventMetadata(
            ReadString(providerName),
            ReadGuid(providerId),
            eventId.ScalarType == WindowsEventNativeMethods.VariantType.Null ? 0 : eventId.UInt16Value,
            ReadUInt16(qualifiers),
            ReadByte(level),
            ReadUInt16(task),
            ReadByte(opcode),
            ReadInt64(keywords),
            ReadFileTime(timeCreated),
            ReadUInt64AsInt64(recordId),
            ReadGuid(activityId),
            ReadGuid(relatedActivityId),
            ReadUInt32AsInt32(processId),
            ReadUInt32AsInt32(threadId),
            ReadString(channel),
            ReadString(computer),
            ReadSid(userId),
            ReadByte(version));
    }

    private static string ReadString(WindowsEventNativeMethods.EventVariant value) {
        return value.ScalarType == WindowsEventNativeMethods.VariantType.Null || value.PointerValue == IntPtr.Zero
            ? string.Empty
            : Marshal.PtrToStringUni(value.PointerValue) ?? string.Empty;
    }

    private static byte? ReadByte(WindowsEventNativeMethods.EventVariant value) {
        return value.ScalarType == WindowsEventNativeMethods.VariantType.Null ? null : value.ByteValue;
    }

    private static ushort? ReadUInt16(WindowsEventNativeMethods.EventVariant value) {
        return value.ScalarType == WindowsEventNativeMethods.VariantType.Null ? null : value.UInt16Value;
    }

    private static int? ReadUInt32AsInt32(WindowsEventNativeMethods.EventVariant value) {
        return value.ScalarType == WindowsEventNativeMethods.VariantType.Null
            ? null
            : unchecked((int)value.UInt32Value);
    }

    private static long? ReadInt64(WindowsEventNativeMethods.EventVariant value) {
        return value.ScalarType == WindowsEventNativeMethods.VariantType.Null ? null : value.Int64Value;
    }

    private static long? ReadUInt64AsInt64(WindowsEventNativeMethods.EventVariant value) {
        return value.ScalarType == WindowsEventNativeMethods.VariantType.Null
            ? null
            : unchecked((long)value.UInt64Value);
    }

    private static DateTime ReadFileTime(WindowsEventNativeMethods.EventVariant value) {
        if (value.ScalarType == WindowsEventNativeMethods.VariantType.Null) {
            return DateTime.MinValue;
        }

        return DateTime.FromFileTimeUtc(unchecked((long)value.UInt64Value)).ToLocalTime();
    }

    private static unsafe Guid? ReadGuid(
        WindowsEventNativeMethods.EventVariant value) {

        return value.ScalarType == WindowsEventNativeMethods.VariantType.Null || value.PointerValue == IntPtr.Zero
            ? null
            : *(Guid*)value.PointerValue;
    }

    private static SecurityIdentifier? ReadSid(WindowsEventNativeMethods.EventVariant value) {
        return value.ScalarType == WindowsEventNativeMethods.VariantType.Null || value.PointerValue == IntPtr.Zero
            ? null
            : new SecurityIdentifier(value.PointerValue);
    }

    public void Dispose() {
        _renderContext.Dispose();
        _buffer.Dispose();
    }
}
