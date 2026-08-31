using System.Diagnostics;

namespace EventViewerX.Reporting;

public static partial class EventNotificationOutbox {
    private const string LockFileName = ".eventviewerx-outbox.lock";

    private static FileStream AcquireWriteLock(
        string outboxDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken) {

        string lockPath = Path.Combine(outboxDirectory, LockFileName);
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
            } catch (IOException) when (stopwatch.Elapsed < timeout) {
                Thread.Sleep(50);
            }
        }
    }

    private static EventNotificationOutboxUsage GetUsage(string root) {
        if (!Directory.Exists(root)) {
            return new EventNotificationOutboxUsage();
        }

        var usage = new EventNotificationOutboxUsage();
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) {
            if (string.Equals(Path.GetFileName(file), LockFileName, StringComparison.Ordinal)) {
                continue;
            }
            usage.TotalBytes += new FileInfo(file).Length;
        }

        foreach (string directory in Directory.GetDirectories(root)) {
            string name = Path.GetFileName(directory);
            long bytes = GetDirectoryBytes(directory);
            if (string.Equals(name, "dead-letter", StringComparison.OrdinalIgnoreCase)) {
                usage.DeadLetterBytes += bytes;
                continue;
            }
            if (name.Contains(".pending-", StringComparison.Ordinal)) {
                usage.StagingBytes += bytes;
                continue;
            }

            EventNotificationDeliveryState delivery = ReadDeliveryState(directory);
            if (delivery.DeliveredUtc.HasValue) {
                usage.DeliveredBytes += bytes;
            } else {
                usage.PendingBatches++;
                usage.PendingBytes += bytes;
            }
        }
        return usage;
    }

    private static long GetDirectoryBytes(string directory) => Directory
        .GetFiles(directory, "*", SearchOption.AllDirectories)
        .Sum(static path => new FileInfo(path).Length);

    private sealed class EventNotificationOutboxUsage {
        internal int PendingBatches { get; set; }
        internal long TotalBytes { get; set; }
        internal long PendingBytes { get; set; }
        internal long DeliveredBytes { get; set; }
        internal long DeadLetterBytes { get; set; }
        internal long StagingBytes { get; set; }
    }
}
