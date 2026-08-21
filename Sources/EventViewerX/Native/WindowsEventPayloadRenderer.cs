using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EventViewerX.Native;

internal sealed class WindowsEventPayloadRenderer : IDisposable {
    private const uint VariantArrayFlag = 0x80;
    private readonly NativeEventBuffer _valueBuffer;
    private readonly WindowsEventXmlRenderer _xmlRenderer;
    private readonly WindowsEventBookmarkRenderer _bookmarkRenderer;
    private readonly WindowsEventNativeMethods.EventHandle _userContext;

    internal WindowsEventPayloadRenderer() {
        WindowsEventNativeMethods.EventHandle userContext =
            WindowsEventNativeMethods.EvtCreateRenderContext(
            0,
            null,
            WindowsEventNativeMethods.RenderContextFlags.User);
        if (userContext.IsInvalid) {
            int error = Marshal.GetLastWin32Error();
            userContext.Dispose();
            throw new System.ComponentModel.Win32Exception(
                error,
                "Failed to create the Windows event payload render context.");
        }

        NativeEventBuffer? valueBuffer = null;
        WindowsEventXmlRenderer? xmlRenderer = null;
        WindowsEventBookmarkRenderer? bookmarkRenderer = null;
        try {
            valueBuffer = new NativeEventBuffer();
            xmlRenderer = new WindowsEventXmlRenderer();
            bookmarkRenderer = new WindowsEventBookmarkRenderer();

            _userContext = userContext;
            _valueBuffer = valueBuffer;
            _xmlRenderer = xmlRenderer;
            _bookmarkRenderer = bookmarkRenderer;
        } catch {
            bookmarkRenderer?.Dispose();
            xmlRenderer?.Dispose();
            valueBuffer?.Dispose();
            userContext.Dispose();
            throw;
        }
    }

    internal NativeEventStructured Render(
        IntPtr eventHandle,
        NativeEventMetadata metadata,
        bool includeBookmark = false) {

        return new NativeEventStructured(
            metadata,
            _xmlRenderer.Render(eventHandle),
            RenderValues(eventHandle),
            includeBookmark
                ? _bookmarkRenderer.Render(eventHandle)
                : null);
    }

    private unsafe IReadOnlyList<EventPropertyValue> RenderValues(
        IntPtr eventHandle) {
        if (!WindowsEventNativeMethods.EvtRender(
                _userContext,
                eventHandle,
                WindowsEventNativeMethods.RenderFlags.EventValues,
                _valueBuffer.Capacity,
                _valueBuffer.Pointer,
                out int bufferUsed,
                out int propertyCount)) {

            int error = Marshal.GetLastWin32Error();
            if (error != WindowsEventNativeMethods.ErrorInsufficientBuffer) {
                throw new System.ComponentModel.Win32Exception(
                    error,
                    "Failed to render Windows event payload values.");
            }

            _valueBuffer.EnsureCapacity(bufferUsed);
            if (!WindowsEventNativeMethods.EvtRender(
                    _userContext,
                    eventHandle,
                    WindowsEventNativeMethods.RenderFlags.EventValues,
                    _valueBuffer.Capacity,
                    _valueBuffer.Pointer,
                    out bufferUsed,
                    out propertyCount)) {
                throw new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to render Windows event payload values.");
            }
        }

        if (propertyCount == 0) {
            return Array.Empty<EventPropertyValue>();
        }

        var values = new EventPropertyValue[propertyCount];
        var variants =
            (WindowsEventNativeMethods.EventVariant*)_valueBuffer.Pointer;
        for (int index = 0; index < propertyCount; index++) {
            values[index] =
                new EventPropertyValue(ReadValue(variants[index]));
        }
        return values;
    }

    internal static object? ReadValue(WindowsEventNativeMethods.EventVariant value) {
        if (value.ScalarType == WindowsEventNativeMethods.VariantType.Null) {
            return null;
        }
        if ((value.Type & VariantArrayFlag) != 0) {
            return ReadArray(value);
        }

        return value.ScalarType switch {
            WindowsEventNativeMethods.VariantType.String =>
                Marshal.PtrToStringUni(value.PointerValue),
            WindowsEventNativeMethods.VariantType.Xml =>
                Marshal.PtrToStringUni(value.PointerValue),
            WindowsEventNativeMethods.VariantType.AnsiString =>
                Marshal.PtrToStringAnsi(value.PointerValue),
            WindowsEventNativeMethods.VariantType.SByte => value.SByteValue,
            WindowsEventNativeMethods.VariantType.Byte => value.ByteValue,
            WindowsEventNativeMethods.VariantType.Int16 => value.Int16Value,
            WindowsEventNativeMethods.VariantType.UInt16 => value.UInt16Value,
            WindowsEventNativeMethods.VariantType.Int32 =>
                value.Int32Value,
            WindowsEventNativeMethods.VariantType.UInt32 or WindowsEventNativeMethods.VariantType.HexInt32 =>
                value.UInt32Value,
            WindowsEventNativeMethods.VariantType.Int64 =>
                value.Int64Value,
            WindowsEventNativeMethods.VariantType.UInt64 or WindowsEventNativeMethods.VariantType.HexInt64 =>
                value.UInt64Value,
            WindowsEventNativeMethods.VariantType.Single => value.SingleValue,
            WindowsEventNativeMethods.VariantType.Double => value.DoubleValue,
            WindowsEventNativeMethods.VariantType.Boolean => value.UInt32Value != 0,
            WindowsEventNativeMethods.VariantType.Binary => CopyBytes(value.PointerValue, value.Count),
            WindowsEventNativeMethods.VariantType.Guid =>
                value.PointerValue == IntPtr.Zero ? null : Marshal.PtrToStructure<Guid>(value.PointerValue),
            WindowsEventNativeMethods.VariantType.SizeT => value.PointerValue,
            WindowsEventNativeMethods.VariantType.FileTime =>
                DateTime.FromFileTimeUtc(unchecked((long)value.UInt64Value)).ToLocalTime(),
            WindowsEventNativeMethods.VariantType.SystemTime =>
                ReadSystemTime(value.PointerValue),
            WindowsEventNativeMethods.VariantType.Sid =>
                value.PointerValue == IntPtr.Zero ? null : new SecurityIdentifier(value.PointerValue),
            WindowsEventNativeMethods.VariantType.EventHandle => value.PointerValue,
            _ => value.PointerValue
        };
    }

    private static object ReadArray(WindowsEventNativeMethods.EventVariant value) {
        int count = checked((int)value.Count);
        if (count == 0 || value.PointerValue == IntPtr.Zero) {
            return Array.Empty<object>();
        }

        return value.ScalarType switch {
            WindowsEventNativeMethods.VariantType.String or WindowsEventNativeMethods.VariantType.Xml =>
                ReadStringArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.AnsiString =>
                ReadAnsiStringArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.SByte =>
                ReadSByteArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.Byte or WindowsEventNativeMethods.VariantType.Binary =>
                CopyBytes(value.PointerValue, value.Count),
            WindowsEventNativeMethods.VariantType.Int16 => ReadInt16Array(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.UInt16 => ReadUInt16Array(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.Int32 => ReadInt32Array(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.UInt32 or WindowsEventNativeMethods.VariantType.HexInt32 =>
                ReadUInt32Array(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.Int64 => ReadInt64Array(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.UInt64 or WindowsEventNativeMethods.VariantType.HexInt64 =>
                ReadUInt64Array(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.Single => ReadSingleArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.Double => ReadDoubleArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.Boolean => ReadBooleanArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.Guid => ReadGuidArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.SizeT or WindowsEventNativeMethods.VariantType.EventHandle =>
                ReadIntPtrArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.FileTime => ReadFileTimeArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.SystemTime => ReadSystemTimeArray(value.PointerValue, count),
            WindowsEventNativeMethods.VariantType.Sid => ReadSidArray(value.PointerValue, count),
            _ => Array.Empty<object>()
        };
    }

    private static byte[] CopyBytes(IntPtr source, uint count) {
        if (source == IntPtr.Zero || count == 0) {
            return Array.Empty<byte>();
        }
        var result = new byte[checked((int)count)];
        Marshal.Copy(source, result, 0, result.Length);
        return result;
    }

    private static string?[] ReadStringArray(IntPtr source, int count) {
        var result = new string?[count];
        for (int index = 0; index < count; index++) {
            IntPtr item = Marshal.ReadIntPtr(source, index * IntPtr.Size);
            result[index] = item == IntPtr.Zero ? null : Marshal.PtrToStringUni(item);
        }
        return result;
    }

    private static string?[] ReadAnsiStringArray(IntPtr source, int count) {
        var result = new string?[count];
        for (int index = 0; index < count; index++) {
            IntPtr item = Marshal.ReadIntPtr(source, index * IntPtr.Size);
            result[index] = item == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(item);
        }
        return result;
    }

    private static sbyte[] ReadSByteArray(IntPtr source, int count) {
        var result = new sbyte[count];
        for (int index = 0; index < count; index++) {
            result[index] = unchecked((sbyte)Marshal.ReadByte(source, index));
        }
        return result;
    }

    private static short[] ReadInt16Array(IntPtr source, int count) {
        var result = new short[count];
        Marshal.Copy(source, result, 0, count);
        return result;
    }

    private static ushort[] ReadUInt16Array(IntPtr source, int count) {
        var result = new ushort[count];
        for (int index = 0; index < count; index++) {
            result[index] = unchecked((ushort)Marshal.ReadInt16(source, index * sizeof(ushort)));
        }
        return result;
    }

    private static uint[] ReadUInt32Array(IntPtr source, int count) {
        var result = new uint[count];
        for (int index = 0; index < count; index++) {
            result[index] = unchecked((uint)Marshal.ReadInt32(source, index * sizeof(uint)));
        }
        return result;
    }

    private static int[] ReadInt32Array(IntPtr source, int count) {
        var result = new int[count];
        Marshal.Copy(source, result, 0, count);
        return result;
    }

    private static long[] ReadInt64Array(IntPtr source, int count) {
        var result = new long[count];
        for (int index = 0; index < count; index++) {
            result[index] = Marshal.ReadInt64(source, index * sizeof(long));
        }
        return result;
    }

    private static ulong[] ReadUInt64Array(IntPtr source, int count) {
        var result = new ulong[count];
        for (int index = 0; index < count; index++) {
            result[index] = unchecked((ulong)Marshal.ReadInt64(source, index * sizeof(ulong)));
        }
        return result;
    }

    private static Guid[] ReadGuidArray(IntPtr source, int count) {
        var result = new Guid[count];
        int size = Marshal.SizeOf<Guid>();
        for (int index = 0; index < count; index++) {
            result[index] = Marshal.PtrToStructure<Guid>(IntPtr.Add(source, index * size));
        }
        return result;
    }

    private static float[] ReadSingleArray(IntPtr source, int count) {
        var result = new float[count];
        Marshal.Copy(source, result, 0, count);
        return result;
    }

    private static double[] ReadDoubleArray(IntPtr source, int count) {
        var result = new double[count];
        Marshal.Copy(source, result, 0, count);
        return result;
    }

    private static bool[] ReadBooleanArray(IntPtr source, int count) {
        var result = new bool[count];
        for (int index = 0; index < count; index++) {
            result[index] = Marshal.ReadInt32(source, index * sizeof(uint)) != 0;
        }
        return result;
    }

    private static IntPtr[] ReadIntPtrArray(IntPtr source, int count) {
        var result = new IntPtr[count];
        Marshal.Copy(source, result, 0, count);
        return result;
    }

    private static DateTime[] ReadFileTimeArray(IntPtr source, int count) {
        long[] values = ReadInt64Array(source, count);
        var result = new DateTime[count];
        for (int index = 0; index < count; index++) {
            result[index] = DateTime.FromFileTimeUtc(values[index]).ToLocalTime();
        }
        return result;
    }

    private static DateTime ReadSystemTime(IntPtr source) {
        if (source == IntPtr.Zero) {
            return DateTime.MinValue;
        }
        WindowsEventNativeMethods.SystemTime value =
            Marshal.PtrToStructure<WindowsEventNativeMethods.SystemTime>(source);
        if (value.Year == 0 || value.Month == 0 || value.Day == 0) {
            return DateTime.MinValue;
        }
        return new DateTime(
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute,
            value.Second,
            value.Milliseconds,
            DateTimeKind.Utc).ToLocalTime();
    }

    private static DateTime[] ReadSystemTimeArray(IntPtr source, int count) {
        var result = new DateTime[count];
        int size = Marshal.SizeOf<WindowsEventNativeMethods.SystemTime>();
        for (int index = 0; index < count; index++) {
            result[index] = ReadSystemTime(IntPtr.Add(source, index * size));
        }
        return result;
    }

    private static SecurityIdentifier?[] ReadSidArray(IntPtr source, int count) {
        var result = new SecurityIdentifier?[count];
        for (int index = 0; index < count; index++) {
            IntPtr item = Marshal.ReadIntPtr(source, index * IntPtr.Size);
            result[index] = item == IntPtr.Zero ? null : new SecurityIdentifier(item);
        }
        return result;
    }

    public void Dispose() {
        _userContext.Dispose();
        _bookmarkRenderer.Dispose();
        _xmlRenderer.Dispose();
        _valueBuffer.Dispose();
    }
}
