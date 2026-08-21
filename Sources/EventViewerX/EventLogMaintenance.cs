using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>Native Windows Event Log maintenance operations.</summary>
public static class EventLogMaintenance {
    /// <summary>Clears a local or remote channel and optionally saves its prior records.</summary>
    public static EventLogClearResult ClearChannel(
        string logName,
        string? machineName = null,
        string? backupPath = null,
        NetworkCredential? credential = null,
        EventLogAuthentication authentication =
            EventLogAuthentication.Default,
        int remoteConnectionTimeoutMilliseconds = 5000,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(logName)) {
            throw new ArgumentException(
                "Event log name cannot be null or empty.",
                nameof(logName));
        }
        if (!Enum.IsDefined(
                typeof(EventLogAuthentication),
                authentication)) {
            throw new ArgumentOutOfRangeException(nameof(authentication));
        }
        if (remoteConnectionTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(remoteConnectionTimeoutMilliseconds),
                "Remote connection timeout must be greater than zero.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        string machine = machineName?.Trim() ?? string.Empty;
        bool remote = !EventLogTarget.IsLocalMachine(machine);
        if (!remote && credential != null) {
            throw new ArgumentException(
                "Credentials can only be used for a remote clear operation.",
                nameof(credential));
        }
        string? normalizedBackup = null;
        if (!string.IsNullOrWhiteSpace(backupPath)) {
            string requestedBackup = backupPath!.Trim().Trim('"', '\'');
            normalizedBackup = remote
                ? requestedBackup
                : Path.GetFullPath(requestedBackup);
        }
        if (!remote && normalizedBackup != null) {
            string? directory = Path.GetDirectoryName(normalizedBackup);
            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory)) {
                throw new DirectoryNotFoundException(
                    $"Backup directory '{directory}' does not exist.");
            }
            if (File.Exists(normalizedBackup)) {
                throw new IOException(
                    $"Backup file '{normalizedBackup}' already exists.");
            }
        }

        RetainedDisposable<
            WindowsEventNativeMethods.EventHandle>?
            sessionLifetime = null;
        try {
            IntPtr sessionHandle = IntPtr.Zero;
            if (remote) {
                sessionLifetime =
                    new RetainedDisposable<
                        WindowsEventNativeMethods.EventHandle>(
                        WindowsEventRemoteSession.OpenBounded(
                            machine,
                            credential,
                            authentication,
                            remoteConnectionTimeoutMilliseconds,
                            cancellationToken));
                sessionHandle =
                    sessionLifetime.Value
                        .DangerousGetHandle();
            }
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedLogName = logName.Trim();
            string targetMachine = remote
                ? machine
                : Environment.MachineName;
            if (remote) {
                ExecuteRemoteClear(
                    () => {
                        ClearChannelNative(
                            sessionHandle,
                            normalizedLogName,
                            normalizedBackup,
                            targetMachine);
                    },
                    remoteConnectionTimeoutMilliseconds,
                    cancellationToken,
                    sessionLifetime!.Retain());
            } else if (cancellationToken.CanBeCanceled) {
                ExecuteLocalClear(
                    () => {
                        ClearChannelNative(
                            sessionHandle,
                            normalizedLogName,
                            normalizedBackup,
                            targetMachine);
                    },
                    cancellationToken);
            } else {
                ClearChannelNative(
                    sessionHandle,
                    normalizedLogName,
                    normalizedBackup,
                    targetMachine);
            }
            return new EventLogClearResult(
                normalizedLogName,
                remote ? machine : null,
                normalizedBackup);
        } finally {
            sessionLifetime?.Dispose();
        }
    }

    internal static void ExecuteLocalClear(
        Action clear,
        CancellationToken cancellationToken) {

        if (clear == null) {
            throw new ArgumentNullException(nameof(clear));
        }
        _ = BoundedNativeOperation.Execute(
            () => {
                clear();
                return true;
            },
            int.MaxValue,
            "The local Windows event channel clear did not complete.",
            cancellationToken);
    }

    internal static void ExecuteRemoteClear(
        Action clear,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        IDisposable operationLease) {

        if (clear == null) {
            operationLease?.Dispose();
            throw new ArgumentNullException(
                nameof(clear));
        }
        _ = EventLogNativeOperation.Execute(
            () => {
                clear();
                return true;
            },
            timeoutMilliseconds,
            $"The remote Windows event channel clear did not complete within {timeoutMilliseconds} ms.",
            cancellationToken,
            operationLease:
                operationLease);
    }

    private static void ClearChannelNative(
        IntPtr sessionHandle,
        string logName,
        string? backupPath,
        string machineName) {

        if (WindowsEventNativeMethods.EvtClearLog(
                sessionHandle,
                logName,
                backupPath,
                0)) {
            return;
        }
        int error = Marshal.GetLastWin32Error();
        throw new Win32Exception(
            error,
            $"Failed to clear Windows event channel '{logName}' on '{machineName}'.");
    }
}
