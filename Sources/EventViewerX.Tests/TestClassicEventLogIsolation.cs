using System.Threading;

namespace EventViewerX.Tests;

internal static class TestClassicEventLogIsolation {
    private const string MutexName =
        @"Local\EventViewerX.Tests.ClassicEventLogMutation";

    public static IDisposable Acquire() {
        var mutex = new Mutex(
            initiallyOwned: false,
            name: MutexName);
        bool ownsMutex = false;

        try {
            try {
                ownsMutex =
                    mutex.WaitOne(TimeSpan.FromSeconds(30));
            } catch (AbandonedMutexException) {
                ownsMutex = true;
            }

            if (!ownsMutex) {
                throw new TimeoutException(
                    "Timed out waiting for exclusive access to Windows classic event-log test state.");
            }

            return new Lease(mutex);
        } catch {
            if (ownsMutex) {
                mutex.ReleaseMutex();
            }

            mutex.Dispose();
            throw;
        }
    }

    private sealed class Lease : IDisposable {
        private Mutex? _mutex;

        public Lease(Mutex mutex) {
            _mutex = mutex;
        }

        public void Dispose() {
            Mutex? mutex =
                Interlocked.Exchange(
                    ref _mutex,
                    null);
            if (mutex is null) {
                return;
            }

            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
