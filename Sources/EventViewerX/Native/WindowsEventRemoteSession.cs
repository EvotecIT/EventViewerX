using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace EventViewerX.Native;

internal static class WindowsEventRemoteSession {
    internal const int EvtOpenSessionReservedTimeout = 0;

    internal static WindowsEventNativeMethods.EventHandle Open(
        string machineName,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        int timeoutMilliseconds) {

        IntPtr password = IntPtr.Zero;
        try {
            if (timeoutMilliseconds <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));
            }
            if (credential?.SecurePassword is { Length: > 0 } securePassword) {
                password = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
            }

            var login = new WindowsEventNativeMethods.RpcLogin {
                Server = machineName,
                User = string.IsNullOrWhiteSpace(credential?.UserName)
                    ? null
                    : credential!.UserName,
                Domain = string.IsNullOrWhiteSpace(credential?.Domain)
                    ? null
                    : credential!.Domain,
                Password = password,
                Flags = (int)authentication
            };
            WindowsEventNativeMethods.EventHandle session =
                WindowsEventNativeMethods.EvtOpenSession(
                    WindowsEventNativeMethods.LoginClass.RpcLogin,
                    ref login,
                    // The native API reserves this argument and requires zero.
                    // Connection bounds are enforced by WindowsEventRemoteReader.
                    EvtOpenSessionReservedTimeout,
                    0);
            if (!session.IsInvalid) {
                return session;
            }

            int error = Marshal.GetLastWin32Error();
            session.Dispose();
            if (error == 5) {
                throw new UnauthorizedAccessException(
                    $"Access was denied opening the Windows Event Log session to '{machineName}'.");
            }
            throw new Win32Exception(
                error,
                $"Failed to open the Windows Event Log session to '{machineName}'.");
        } finally {
            if (password != IntPtr.Zero) {
                Marshal.ZeroFreeGlobalAllocUnicode(password);
            }
        }
    }

    internal static WindowsEventNativeMethods.EventHandle OpenBounded(
        string machineName,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        int timeoutMilliseconds,
        CancellationToken cancellationToken) {

        string timeoutMessage =
            $"Timed out opening the Windows Event Log session to '{machineName}' after {timeoutMilliseconds} ms.";
        return BoundedNativeOperation.Execute(
            () => Open(
                machineName,
                credential,
                authentication,
                timeoutMilliseconds),
            timeoutMilliseconds,
            timeoutMessage,
            cancellationToken,
            static lateSession => lateSession.Dispose());
    }
}
