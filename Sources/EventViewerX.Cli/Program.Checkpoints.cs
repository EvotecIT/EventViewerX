using EventViewerX.Reporting;
using EventViewerX.Storage;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static async Task<CollectionCheckpointContext?>
        PrepareCollectionCheckpointAsync(
            EventReportRequest request,
            CliArguments options) {

        string? consumer = options.Get("checkpoint");
        if (consumer == null) {
            return null;
        }
        if (string.IsNullOrWhiteSpace(consumer)) {
            throw new ArgumentException("--checkpoint requires a non-empty consumer name.");
        }
        string storePath = options.Get("write-store") ??
            throw new ArgumentException(
                "--checkpoint requires --write-store so events and progress commit atomically.");
        if (options.Has("explain")) {
            throw new ArgumentException(
                "--checkpoint cannot be combined with --explain because explanation does not read or persist events.");
        }
        if (options.GetLong("max") > 0) {
            throw new ArgumentException(
                "--checkpoint cannot be combined with --max because a result-limited query cannot advance durable collection progress safely.");
        }
        if (options.Has("end")) {
            throw new ArgumentException(
                "--checkpoint cannot be combined with --end because durable collection owns its upper record boundary.");
        }
        if (request.Collectors == null || request.Collectors.Count != 1 ||
            request.MachineNames != null && request.MachineNames.Count > 0 ||
            request.Paths != null && request.Paths.Count > 0) {
            throw new ArgumentException(
                "--checkpoint currently requires exactly one --collector target and does not support direct machines or offline files.");
        }

        string? target = request.Collectors[0];
        string computer = NormalizeCheckpointComputer(target);
        string container = request.CollectorLogName;
        var store = new EventStore(storePath);
        EventStoreCheckpoint? saved = await store.GetCheckpointAsync(
            consumer,
            computer,
            container).ConfigureAwait(false);
        EventLogRecordRange range = ReadRecordRange(target, container);
        saved?.ValidateAvailableRange(
            range.OldestRecordId,
            range.NewestRecordId);

        if (saved != null) {
            request.StartTime = null;
            request.TimePeriod = null;
            string? bookmarkXml = saved.BookmarkXml;
            if (string.IsNullOrWhiteSpace(bookmarkXml) &&
                saved.RecordId.HasValue) {
                bookmarkXml = ReadCheckpointBookmark(
                    target,
                    container,
                    saved.RecordId.Value);
            }
            if (!string.IsNullOrWhiteSpace(bookmarkXml)) {
                string resolvedBookmarkXml = bookmarkXml!;
                request.BookmarkXmlResolver =
                    (_, _) => resolvedBookmarkXml;
            }
        } else if (!request.StartTime.HasValue && !request.TimePeriod.HasValue) {
            throw new ArgumentException(
                "The first --checkpoint run requires --since or --start to declare the intentional initial backfill window.");
        }
        request.Oldest = true;

        return new CollectionCheckpointContext(
            store,
            new EventStoreCheckpoint {
                Consumer = consumer.Trim(),
                Computer = computer,
                Container = container,
                RecordId = range.NewestRecordId,
                BookmarkXml = range.NewestBookmarkXml
            },
            saved,
            target,
            range);
    }

    private static EventLogRecordRange ReadRecordRange(
        string? machineName,
        string logName) {

        EventObject? oldest = ReadBoundaryEvent(
            machineName,
            logName,
            oldest: true);
        EventObject? newest = ReadBoundaryEvent(
            machineName,
            logName,
            oldest: false);
        if ((oldest == null) != (newest == null)) {
            throw new InvalidDataException(
                $"The retained record range for '{logName}' on '{NormalizeCheckpointComputer(machineName)}' changed while it was inspected; retry collection before advancing the checkpoint.");
        }
        if (oldest != null &&
            (!oldest.RecordId.HasValue || !newest!.RecordId.HasValue ||
             oldest.RecordId.Value < 0 ||
             newest.RecordId.Value < oldest.RecordId.Value ||
             string.IsNullOrWhiteSpace(newest.BookmarkXml))) {
            throw new InvalidDataException(
                $"The retained record range for '{logName}' on '{NormalizeCheckpointComputer(machineName)}' is inconsistent; retry collection before advancing the checkpoint.");
        }
        return new EventLogRecordRange(
            oldest?.RecordId,
            newest?.RecordId,
            newest?.BookmarkXml);
    }

    private static EventObject? ReadBoundaryEvent(
        string? machineName,
        string logName,
        bool oldest) {

        EventObject? boundary = EventLogEngine.ReadChannel(
            new EventLogChannelQuery(logName) {
                MachineName = machineName,
                XPath = "*",
                Oldest = oldest,
                MaxEvents = 1,
                ReadMode = EventReadMode.Metadata,
                IncludeBookmark = true,
                RemoteConnectionTimeoutMilliseconds = 5000,
                RemoteReadTimeoutMilliseconds = 5000
            }).FirstOrDefault();
        return boundary;
    }

    private static string ReadCheckpointBookmark(
        string? machineName,
        string logName,
        long recordId) {

        EventObject? boundary = EventLogEngine.ReadChannel(
            new EventLogChannelQuery(logName) {
                MachineName = machineName,
                XPath = $"*[System[EventRecordID={recordId}]]",
                Oldest = true,
                MaxEvents = 1,
                ReadMode = EventReadMode.Metadata,
                IncludeBookmark = true,
                RemoteConnectionTimeoutMilliseconds = 5000,
                RemoteReadTimeoutMilliseconds = 5000
            }).FirstOrDefault();
        if (boundary?.RecordId != recordId ||
            string.IsNullOrWhiteSpace(boundary.BookmarkXml)) {
            throw new InvalidDataException(
                $"Checkpoint {recordId} for '{logName}' on '{NormalizeCheckpointComputer(machineName)}' cannot be converted to a native bookmark; no events or checkpoint were committed.");
        }
        return boundary.BookmarkXml!;
    }

    private static string NormalizeCheckpointComputer(string? machineName) =>
        EventLogTarget.IsLocalMachine(machineName)
            ? EventLogTarget.LocalMachineName
            : machineName!.Trim().TrimEnd('.');

    private static async Task WriteCheckpointedStoreAsync(
        EventReport report,
        CollectionCheckpointContext context) {

        EventLogRecordRange currentRange = ReadRecordRange(
            context.MachineName,
            context.NextCheckpoint.Container);
        context.SavedCheckpoint?.ValidateAvailableRange(
            currentRange.OldestRecordId,
            currentRange.NewestRecordId);
        if (context.CapturedRange.NewestRecordId.HasValue &&
            (!currentRange.NewestRecordId.HasValue ||
             currentRange.NewestRecordId.Value <
             context.CapturedRange.NewestRecordId.Value)) {
            throw new InvalidDataException(
                "The collector channel was cleared or replaced while collection was running; no events or checkpoint were committed.");
        }
        if (context.CapturedRange.OldestRecordId.HasValue &&
            currentRange.OldestRecordId.HasValue &&
            currentRange.OldestRecordId.Value <
            context.CapturedRange.OldestRecordId.Value) {
            throw new InvalidDataException(
                "The collector channel's retained record range moved backwards while collection was running; no events or checkpoint were committed.");
        }
        if (report.ScanLimitReached) {
            throw new InvalidDataException(
                "The collection candidate limit was reached; no events or checkpoint were committed. Increase --max-candidates and retry.");
        }
        if (report.Coverage.Count == 0) {
            throw new InvalidDataException(
                "Collection returned no source-coverage evidence; no events or checkpoint were committed.");
        }
        EventReportCoverage[] failures = report.Coverage
            .Where(static coverage => !coverage.Succeeded)
            .ToArray();
        if (failures.Length > 0) {
            throw new InvalidDataException(
                "Collection was incomplete for " +
                string.Join(
                    ", ",
                    failures.Select(static failure =>
                        $"{failure.MachineName}/{failure.LogName} ({failure.Status})")) +
                "; no events or checkpoint were committed.");
        }

        EventStoreWriteResult result = await context.Store
            .WriteAsync(
                report,
                context.NextCheckpoint,
                context.SavedCheckpoint)
            .ConfigureAwait(false);
        Console.Error.WriteLine(
            $"Stored {result.Inserted} new rows; skipped {result.Duplicates} duplicates and committed checkpoint '{context.NextCheckpoint.Consumer}' at record {context.NextCheckpoint.RecordId?.ToString() ?? "<empty>"} in {Path.GetFullPath(context.Store.Path)}.");
    }

    private sealed class CollectionCheckpointContext {
        internal CollectionCheckpointContext(
            EventStore store,
            EventStoreCheckpoint nextCheckpoint,
            EventStoreCheckpoint? savedCheckpoint,
            string? machineName,
            EventLogRecordRange capturedRange) {

            Store = store;
            NextCheckpoint = nextCheckpoint;
            SavedCheckpoint = savedCheckpoint;
            MachineName = machineName;
            CapturedRange = capturedRange;
        }

        internal EventStore Store { get; }
        internal EventStoreCheckpoint NextCheckpoint { get; }
        internal EventStoreCheckpoint? SavedCheckpoint { get; }
        internal string? MachineName { get; }
        internal EventLogRecordRange CapturedRange { get; }
    }

    private readonly struct EventLogRecordRange {
        internal EventLogRecordRange(
            long? oldestRecordId,
            long? newestRecordId,
            string? newestBookmarkXml) {

            OldestRecordId = oldestRecordId;
            NewestRecordId = newestRecordId;
            NewestBookmarkXml = newestBookmarkXml;
        }

        internal long? OldestRecordId { get; }
        internal long? NewestRecordId { get; }
        internal string? NewestBookmarkXml { get; }
    }
}
