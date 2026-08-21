using System.Security.Cryptography;
using System.Text;

namespace EventViewerX.Providers;

internal sealed class EventProviderLifecycleLock : IDisposable {
    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private EventProviderLifecycleLock(
        Mutex mutex,
        bool ownsMutex) {

        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    internal static EventProviderLifecycleLock Acquire(
        Guid providerId,
        TimeSpan timeout) {

        if (providerId == Guid.Empty) {
            throw new ArgumentException(
                "A provider GUID is required for lifecycle serialization.",
                nameof(providerId));
        }
        return Acquire(
            "Global\\EventViewerX.Provider." +
            providerId.ToString("N"),
            timeout,
            $"provider {providerId:D}");
    }

    internal static EventProviderLifecycleLock AcquireProviderName(
        string providerName,
        TimeSpan timeout) {

        if (string.IsNullOrWhiteSpace(providerName)) {
            throw new ArgumentException(
                "A provider name is required for lifecycle serialization.",
                nameof(providerName));
        }
        string normalized =
            providerName.Trim().ToUpperInvariant();
        byte[] hash;
        using (SHA256 sha256 = SHA256.Create()) {
            hash = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(normalized));
        }
        string key =
            BitConverter.ToString(hash)
                .Replace("-", string.Empty);
        return Acquire(
            "Global\\EventViewerX.ProviderName." + key,
            timeout,
            $"provider name '{providerName.Trim()}'");
    }

    internal static EventProviderLifecycleLock AcquireProviderRoot(
        string rootPath,
        TimeSpan timeout) {

        if (string.IsNullOrWhiteSpace(rootPath)) {
            throw new ArgumentException(
                "A provider root is required for lifecycle serialization.",
                nameof(rootPath));
        }
        string normalized =
            Path.GetFullPath(rootPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
        byte[] hash;
        using (SHA256 sha256 = SHA256.Create()) {
            hash = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(normalized));
        }
        string key =
            BitConverter.ToString(hash)
                .Replace("-", string.Empty);
        return Acquire(
            "Global\\EventViewerX.ProviderRoot." + key,
            timeout,
            $"provider root '{rootPath}'");
    }

    private static EventProviderLifecycleLock Acquire(
        string mutexName,
        TimeSpan timeout,
        string description) {

        if (timeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Lifecycle lock timeout must be positive.");
        }

        var mutex = new Mutex(
            initiallyOwned: false,
            mutexName);
        bool acquired;
        try {
            acquired = mutex.WaitOne(timeout);
        } catch (AbandonedMutexException) {
            acquired = true;
        }
        if (!acquired) {
            mutex.Dispose();
            throw new TimeoutException(
                $"Timed out waiting for another lifecycle operation on {description}.");
        }
        return new EventProviderLifecycleLock(
            mutex,
            ownsMutex: true);
    }

    public void Dispose() {
        try {
            if (_ownsMutex) {
                _ownsMutex = false;
                _mutex.ReleaseMutex();
            }
        } finally {
            _mutex.Dispose();
        }
    }
}
