using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EventViewerX;

/// <summary>Owns file replacement and recovery across cooperating processes.</summary>
internal static class FilePublication {
    internal static IDisposable AcquireOwnership(string destination, CancellationToken cancellationToken = default) {
        string identity = FileSystemPathIdentity.GetIdentity(destination);
        string hash;
        using (var algorithm = SHA256.Create()) {
            hash = BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", string.Empty);
        }
        string name = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"Global\" : string.Empty) + "EventViewerX.FilePublication." + hash;
        var mutex = new Mutex(false, name);
        try {
            var elapsed = Stopwatch.StartNew();
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                bool acquired;
                try {
                    acquired = mutex.WaitOne(50);
                } catch (AbandonedMutexException) {
                    // The prior process exited. Its staged recovery files remain for inspection.
                    acquired = true;
                }
                if (acquired) {
                    return new Ownership(mutex);
                }
                if (elapsed.Elapsed >= TimeSpan.FromSeconds(30)) {
                    throw new TimeoutException($"Timed out waiting to publish '{destination}'.");
                }
            }
        } catch {
            mutex.Dispose();
            throw;
        }
    }

    internal static void Promote(string temporaryPath, string destination, bool overwrite) {
        using (AcquireOwnership(destination)) {
            if (!overwrite) {
                File.Move(temporaryPath, destination);
                return;
            }
            if (!File.Exists(destination)) {
                try {
                    File.Move(temporaryPath, destination);
                    return;
                } catch (IOException) when (File.Exists(destination)) {
                    // A non-cooperating writer can create the destination after the check.
                }
            }
            string backup = temporaryPath + ".recovery";
            try {
                // A native backup prevents ERROR_UNABLE_TO_MOVE_REPLACEMENT from
                // discarding the old file. It also retains metadata needed for recovery.
                File.Replace(temporaryPath, destination, backup);
            } catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException) {
                RestoreFailedReplacement(destination, backup, failure);
                throw;
            }
            DeleteBackupBestEffort(backup);
        }
    }

    /// <summary>Recovers the documented partial native replacement state without overwriting an unknown destination.</summary>
    internal static void RestoreFailedReplacement(string destination, string backup, Exception failure) {
        if (!File.Exists(backup)) {
            return;
        }
        try {
            // Error 1177 may have moved the old destination to the native backup.
            // A failed recovery must leave that backup intact, including its streams/ACLs.
            File.Move(backup, destination);
        } catch (Exception recoveryFailure) when (recoveryFailure is IOException || recoveryFailure is UnauthorizedAccessException) {
            throw new FilePublicationRecoveryException(destination, backup, failure, recoveryFailure);
        }
    }

    private static void DeleteBackupBestEffort(string path) {
        try {
            File.Delete(path);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private sealed class Ownership : IDisposable {
        private Mutex? mutex;
        internal Ownership(Mutex mutex) => this.mutex = mutex;
        public void Dispose() {
            Mutex? owned = mutex;
            if (owned != null) {
                mutex = null;
                owned.ReleaseMutex();
                owned.Dispose();
            }
        }
    }
}

/// <summary>Reports retained recovery data when a failed file replacement cannot be restored.</summary>
internal sealed class FilePublicationRecoveryException : IOException {
    internal FilePublicationRecoveryException(string destination, string backup, Exception failure, Exception recoveryFailure)
        : base($"Publication of '{destination}' failed and the original could not be restored. Recovery data remains at '{backup}'.",
            new AggregateException(failure, recoveryFailure)) { }
}
