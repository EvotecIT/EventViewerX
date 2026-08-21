using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EventViewerX.Native;

internal static class WindowsEventQueryDiagnostics {
    internal static void ReportFailures(
        WindowsEventNativeMethods.EventHandle query,
        NativeEventQuery eventQuery) {

        if ((eventQuery.Flags &
             WindowsEventNativeMethods.QueryFlags.TolerateQueryErrors) == 0) {
            return;
        }

        string[] names = ReadStringArray(
            query,
            WindowsEventNativeMethods.QueryPropertyId.Names);
        uint[] statuses = ReadUInt32Array(
            query,
            WindowsEventNativeMethods.QueryPropertyId.Statuses);
        int count = Math.Min(names.Length, statuses.Length);
        var failures = new List<EventLogQueryFailure>();
        for (int index = 0; index < count; index++) {
            int status = unchecked((int)statuses[index]);
            if (status == 0) {
                continue;
            }

            string source = string.IsNullOrWhiteSpace(names[index])
                ? eventQuery.DisplayName
                : names[index];
            failures.Add(new EventLogQueryFailure(
                source,
                eventQuery.MachineName,
                new Win32Exception(
                    status,
                    $"Windows could not evaluate event query path '{source}'.")));
        }

        if (failures.Count == 0) {
            return;
        }
        if (eventQuery.FailureHandler == null) {
            throw new EventLogStructuredQueryException(failures);
        }
        foreach (EventLogQueryFailure failure in failures) {
            eventQuery.FailureHandler(failure);
        }
    }

    private static string[] ReadStringArray(
        WindowsEventNativeMethods.EventHandle query,
        WindowsEventNativeMethods.QueryPropertyId propertyId) {

        return ReadProperty(
            query,
            propertyId,
            static value => DecodeStringArray(value));
    }

    private static string[] DecodeStringArray(
        WindowsEventNativeMethods.EventVariant value) {

        if (value.ScalarType !=
                WindowsEventNativeMethods.VariantType.String ||
            !value.IsArray ||
            value.PointerValue == IntPtr.Zero ||
            value.Count == 0) {
            return Array.Empty<string>();
        }

        var values = new string[value.Count];
        for (int index = 0; index < values.Length; index++) {
            IntPtr pointer = Marshal.ReadIntPtr(
                value.PointerValue,
                index * IntPtr.Size);
            values[index] = pointer == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUni(pointer) ?? string.Empty;
        }
        return values;
    }

    private static uint[] ReadUInt32Array(
        WindowsEventNativeMethods.EventHandle query,
        WindowsEventNativeMethods.QueryPropertyId propertyId) {

        return ReadProperty(
            query,
            propertyId,
            static value => DecodeUInt32Array(value));
    }

    private static uint[] DecodeUInt32Array(
        WindowsEventNativeMethods.EventVariant value) {

        if (value.ScalarType !=
                WindowsEventNativeMethods.VariantType.UInt32 ||
            !value.IsArray ||
            value.PointerValue == IntPtr.Zero ||
            value.Count == 0) {
            return Array.Empty<uint>();
        }

        var values = new uint[value.Count];
        for (int index = 0; index < values.Length; index++) {
            values[index] = unchecked((uint)Marshal.ReadInt32(
                value.PointerValue,
                index * sizeof(uint)));
        }
        return values;
    }

    private static TResult ReadProperty<TResult>(
        WindowsEventNativeMethods.EventHandle query,
        WindowsEventNativeMethods.QueryPropertyId propertyId,
        Func<WindowsEventNativeMethods.EventVariant, TResult> decode) {

        _ = WindowsEventNativeMethods.EvtGetQueryInfo(
            query,
            propertyId,
            0,
            IntPtr.Zero,
            out int bufferSize);
        int error = Marshal.GetLastWin32Error();
        if (bufferSize <= 0 ||
            error != WindowsEventNativeMethods.ErrorInsufficientBuffer) {
            throw new Win32Exception(
                error,
                $"Failed to read Windows event query property '{propertyId}'.");
        }

        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try {
            if (!WindowsEventNativeMethods.EvtGetQueryInfo(
                    query,
                    propertyId,
                    bufferSize,
                    buffer,
                    out _)) {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"Failed to read Windows event query property '{propertyId}'.");
            }
            WindowsEventNativeMethods.EventVariant value =
                Marshal.PtrToStructure<
                    WindowsEventNativeMethods.EventVariant>(buffer);
            return decode(value);
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
