using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EventViewerX.Native;

internal static class WindowsEventCollector {
    private const uint WriteAccess = 2;
    // EC_OPEN_EXISTING from the Windows SDK EvColl.h.
    private const uint OpenExisting = 2;
    private const uint ReservedFlags = 0;

    internal static void SetEnabled(
        string subscriptionName,
        bool enabled) {

        using CollectorHandle subscription =
            NativeMethods.EcOpenSubscription(
                subscriptionName,
                WriteAccess,
                OpenExisting);
        if (subscription.IsInvalid) {
            throw CreateException(
                $"Failed to open collector subscription '{subscriptionName}' for writing");
        }

        var value = new CollectorVariant {
            Value = new CollectorVariantValue {
                Boolean = enabled ? 1 : 0
            },
            Type =
                CollectorVariantType.Boolean
        };
        if (!NativeMethods
            .EcSetSubscriptionProperty(
                subscription,
                CollectorSubscriptionProperty.Enabled,
                ReservedFlags,
                ref value)) {
            throw CreateException(
                $"Failed to set Enabled={enabled} on collector subscription '{subscriptionName}'");
        }
        if (!NativeMethods.EcSaveSubscription(
                subscription,
                ReservedFlags)) {
            throw CreateException(
                $"Failed to save collector subscription '{subscriptionName}'");
        }
    }

    private static Win32Exception CreateException(
        string operation) {

        int error = Marshal.GetLastWin32Error();
        return new Win32Exception(
            error,
            $"{operation}. Windows error {error}.");
    }

    private enum CollectorSubscriptionProperty : uint {
        Enabled = 0
    }

    private enum CollectorVariantType : uint {
        Null = 0,
        Boolean = 1
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CollectorVariantValue {
        [FieldOffset(0)]
        internal int Boolean;
        [FieldOffset(0)]
        internal ulong DateTime;
        [FieldOffset(0)]
        internal IntPtr Pointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CollectorVariant {
        internal CollectorVariantValue Value;
        internal uint Count;
        internal CollectorVariantType Type;
    }

    private sealed class CollectorHandle :
        SafeHandleZeroOrMinusOneIsInvalid {

        private CollectorHandle()
            : base(ownsHandle: true) {
        }

        protected override bool ReleaseHandle() {
            return NativeMethods.EcClose(handle);
        }
    }

    private static class NativeMethods {
        [DllImport(
            "wecapi.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        internal static extern CollectorHandle
            EcOpenSubscription(
                string subscriptionName,
                uint accessMask,
                uint flags);

        [DllImport(
            "wecapi.dll",
            SetLastError = true)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        internal static extern bool
            EcSetSubscriptionProperty(
                CollectorHandle subscription,
                CollectorSubscriptionProperty propertyId,
                uint flags,
                ref CollectorVariant propertyValue);

        [DllImport(
            "wecapi.dll",
            SetLastError = true)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        internal static extern bool
            EcSaveSubscription(
                CollectorHandle subscription,
                uint flags);

        [DllImport(
            "wecapi.dll",
            SetLastError = true)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        internal static extern bool EcClose(
            IntPtr handle);
    }
}
