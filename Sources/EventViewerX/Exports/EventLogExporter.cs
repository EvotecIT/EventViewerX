using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using EventViewerX.Exports;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Writes Windows events directly to durable streaming formats without a PowerShell object pipeline.
/// </summary>
public static class EventLogExporter {
    /// <summary>
    /// Exports an offline event query to a file and atomically promotes the completed output.
    /// </summary>
    /// <param name="query">File query and projection options.</param>
    /// <param name="outputPath">Destination file.</param>
    /// <param name="format">Streaming output format.</param>
    /// <param name="overwrite">Whether an existing destination may be replaced after a successful export.</param>
    /// <param name="cancellationToken">Token used to cancel enumeration and leave the existing destination unchanged.</param>
    /// <param name="computeSha256">Whether to hash the completed temporary output before atomic promotion.</param>
    /// <param name="archiveResources">Whether native EVTX output should embed provider resources for portable message rendering.</param>
    /// <returns>Count, size, and SHA-256 for the completed output.</returns>
    public static EventExportResult ExportFile(
        EventLogFileQuery query,
        string outputPath,
        EventExportFormat format,
        bool overwrite = false,
        CancellationToken cancellationToken = default,
        bool computeSha256 = true,
        bool archiveResources = false) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }

        string destination = ResolveDestination(outputPath);
        string source = Path.GetFullPath(query.Path.Trim().Trim('"', '\''));
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) {
            throw new IOException("The export destination cannot overwrite the source event log.");
        }
        if (format == EventExportFormat.Evtx) {
            return ExportEvtxCore(
                destination,
                overwrite,
                computeSha256,
                cancellationToken,
                temporaryPath => WindowsEventArchive.ExportFile(
                    query,
                    temporaryPath,
                    archiveResources,
                    cancellationToken));
        }

        return ExportCore(
            destination,
            format,
            overwrite,
            computeSha256,
            cancellationToken,
            stream => WriteFile(query, format, stream, cancellationToken));
    }

    /// <summary>
    /// Exports a local or remote channel query to a file and atomically promotes the completed output.
    /// </summary>
    /// <param name="query">Channel query and projection options.</param>
    /// <param name="outputPath">Destination file.</param>
    /// <param name="format">Streaming output format.</param>
    /// <param name="overwrite">Whether an existing destination may be replaced after a successful export.</param>
    /// <param name="cancellationToken">Token used to cancel enumeration and leave the existing destination unchanged.</param>
    /// <param name="computeSha256">Whether to hash the completed temporary output before atomic promotion.</param>
    /// <param name="archiveResources">Whether native EVTX output should embed provider resources for portable message rendering.</param>
    /// <returns>Count, size, and SHA-256 for the completed output.</returns>
    public static EventExportResult ExportChannel(
        EventLogChannelQuery query,
        string outputPath,
        EventExportFormat format,
        bool overwrite = false,
        CancellationToken cancellationToken = default,
        bool computeSha256 = true,
        bool archiveResources = false) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }

        string destination = ResolveDestination(outputPath);
        if (format == EventExportFormat.Evtx) {
            ValidateLocalNativeExportTarget(query.MachineName);
            return ExportEvtxCore(
                destination,
                overwrite,
                computeSha256,
                cancellationToken,
                temporaryPath => WindowsEventArchive.ExportChannel(
                    query,
                    temporaryPath,
                    archiveResources,
                    cancellationToken));
        }

        return ExportCore(
            destination,
            format,
            overwrite,
            computeSha256,
            cancellationToken,
            stream => WriteChannel(query, format, stream, cancellationToken));
    }

    /// <summary>
    /// Exports a structured multi-channel or multi-file QueryList atomically.
    /// </summary>
    public static EventExportResult ExportStructured(
        EventLogStructuredQuery query,
        string outputPath,
        EventExportFormat format,
        bool overwrite = false,
        CancellationToken cancellationToken = default,
        bool computeSha256 = true,
        bool archiveResources = false) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        string destination = ResolveDestination(outputPath);
        ValidateDestinationDoesNotOverwriteSources(
            destination,
            query.ResolveSources());
        if (format == EventExportFormat.Evtx) {
            ValidateLocalNativeExportTarget(query.MachineName);
            if (query.TolerateQueryErrors) {
                EventLogStructuredQuery preflight =
                    CopyStructuredQuery(
                        query,
                        EventReadMode.Metadata);
                preflight.MaxEvents = 1;
                using IEnumerator<EventObject> events =
                    EventLogEngine.ReadStructured(
                        preflight,
                        cancellationToken)
                    .GetEnumerator();
                _ = events.MoveNext();
            }
            return ExportEvtxCore(
                destination,
                overwrite,
                computeSha256,
                cancellationToken,
                temporaryPath =>
                    WindowsEventArchive.ExportStructured(
                        query,
                        temporaryPath,
                        archiveResources,
                        cancellationToken));
        }
        return ExportCore(
            destination,
            format,
            overwrite,
            computeSha256,
            cancellationToken,
            stream => WriteStructured(
                query,
                format,
                stream,
                cancellationToken));
    }

    /// <summary>
    /// Exports a deterministic multi-source batch directly to CSV, JSON Lines, or XML.
    /// The merge retains only one detached event per source in memory.
    /// </summary>
    /// <remarks>
    /// Native EVTX is intentionally not supported because Windows can export a structured
    /// multi-channel query but cannot merge independent files or sessions into one EVTX.
    /// Use <see cref="ExportStructured"/> for a native multi-channel QueryList export.
    /// </remarks>
    public static EventExportResult ExportBatch(
        EventLogBatchQuery query,
        string outputPath,
        EventExportFormat format,
        bool overwrite = false,
        CancellationToken cancellationToken = default,
        bool computeSha256 = true) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (format == EventExportFormat.Evtx) {
            throw new NotSupportedException(
                "A merged batch cannot be represented as one native EVTX. Use a structured multi-channel export or export each source separately.");
        }
        string destination = ResolveDestination(outputPath);
        ValidateDestinationDoesNotOverwriteSources(
            destination,
            query.FileQueries
                .Select(static file =>
                    new EventLogStructuredQuerySource(
                        EventLogQuerySourceKind.File,
                        Path.GetFullPath(
                            file.Path.Trim().Trim('"', '\''))))
                .Concat(query.StructuredQueries.SelectMany(
                    static structured =>
                        structured.ResolveSources())));
        return ExportCore(
            destination,
            format,
            overwrite,
            computeSha256,
            cancellationToken,
            stream => WriteBatch(
                query,
                format,
                stream,
                cancellationToken));
    }

    internal static EventExportResult ExportEvtxCore(
        string destination,
        bool overwrite,
        bool computeSha256,
        CancellationToken cancellationToken,
        Action<string> export) {

        string? directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) {
            throw new DirectoryNotFoundException(
                $"Output directory '{directory}' does not exist.");
        }
        if (File.Exists(destination) && !overwrite) {
            throw new IOException(
                $"Output file '{destination}' already exists.");
        }

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp.evtx");
        bool cleanupDeferred = false;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                _ = BoundedNativeOperation.Execute(
                    () => {
                        try {
                            export(temporaryPath);
                            return true;
                        } catch {
                            DeleteTemporaryFile(temporaryPath);
                            throw;
                        }
                    },
                    int.MaxValue,
                    $"Native EVTX export to '{destination}' did not complete.",
                    cancellationToken,
                    _ => DeleteTemporaryFile(temporaryPath));
            } catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested) {
                cleanupDeferred = true;
                throw;
            }
            cancellationToken.ThrowIfCancellationRequested();
            long count = WindowsEventArchive.GetFileRecordCount(temporaryPath);
            long bytes = new FileInfo(temporaryPath).Length;
            string? sha256 = computeSha256
                ? ComputeSha256(temporaryPath, cancellationToken)
                : null;
            cancellationToken.ThrowIfCancellationRequested();
            PromoteTemporaryFile(temporaryPath, destination, overwrite);
            return new EventExportResult(
                destination,
                EventExportFormat.Evtx,
                count,
                bytes,
                sha256);
        } finally {
            if (!cleanupDeferred) {
                DeleteTemporaryFile(temporaryPath);
            }
        }
    }

    internal static void DeleteTemporaryFile(
        string temporaryPath,
        Action<string>? delete = null) {

        try {
            if (File.Exists(temporaryPath)) {
                (delete ?? File.Delete)(temporaryPath);
            }
        } catch (IOException) {
            // A canceled native export retains ownership until its worker
            // completes and invokes this cleanup again.
        } catch (UnauthorizedAccessException) {
            // Preserve the caller's authoritative export failure.
        }
    }

    internal static EventExportResult ExportCore(
        string destination,
        EventExportFormat format,
        bool overwrite,
        bool computeSha256,
        CancellationToken cancellationToken,
        Func<Stream, long> write) {

        string? directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) {
            throw new DirectoryNotFoundException($"Output directory '{directory}' does not exist.");
        }
        if (File.Exists(destination) && !overwrite) {
            throw new IOException($"Output file '{destination}' already exists.");
        }

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        long count;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       1024 * 1024,
                       FileOptions.SequentialScan)) {
                count = write(stream);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var temporaryInfo = new FileInfo(temporaryPath);
            long bytes = temporaryInfo.Length;
            string? sha256 = computeSha256
                ? ComputeSha256(temporaryPath, cancellationToken)
                : null;
            cancellationToken.ThrowIfCancellationRequested();
            PromoteTemporaryFile(temporaryPath, destination, overwrite);
            return new EventExportResult(destination, format, count, bytes, sha256);
        } finally {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    internal static void PromoteTemporaryFile(
        string temporaryPath,
        string destination,
        bool overwrite) {

        if (!overwrite) {
            File.Move(temporaryPath, destination);
            return;
        }

        if (File.Exists(destination)) {
            File.Replace(temporaryPath, destination, null);
            return;
        }

        try {
            File.Move(temporaryPath, destination);
        } catch (IOException) when (File.Exists(destination)) {
            File.Replace(temporaryPath, destination, null);
        }
    }

    private static long WriteFile(
        EventLogFileQuery query,
        EventExportFormat format,
        Stream stream,
        CancellationToken cancellationToken) {

        if (format == EventExportFormat.Xml) {
            using var writer = new EventXmlWriter(stream);
            long count = EventLogEngine.CopyFileXml(
                query,
                writer.EventStream,
                cancellationToken);
            writer.Complete();
            return count;
        }

        return WriteProjectedEvents(
            EventLogEngine.ReadFile(query, cancellationToken),
            format,
            stream,
            cancellationToken);
    }

    private static long WriteChannel(
        EventLogChannelQuery query,
        EventExportFormat format,
        Stream stream,
        CancellationToken cancellationToken) {

        if (format != EventExportFormat.Xml) {
            return WriteProjectedEvents(
                EventLogEngine.ReadChannel(query, cancellationToken),
                format,
                stream,
                cancellationToken);
        }

        EventLogChannelQuery xmlQuery = CopyChannelQuery(
            query,
            EventReadMode.RawXml);
        long count = 0;
        using var writer = new EventXmlWriter(stream);
        foreach (EventObject eventObject in EventLogEngine.ReadChannel(
                     xmlQuery,
                     cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteXml(eventObject.XMLData);
            count++;
        }
        writer.Complete();
        return count;
    }

    private static long WriteStructured(
        EventLogStructuredQuery query,
        EventExportFormat format,
        Stream stream,
        CancellationToken cancellationToken) {

        if (format != EventExportFormat.Xml) {
            return WriteProjectedEvents(
                EventLogEngine.ReadStructured(
                    query,
                    cancellationToken),
                format,
                stream,
                cancellationToken);
        }

        EventLogStructuredQuery xmlQuery =
            CopyStructuredQuery(
                query,
                EventReadMode.RawXml);
        long count = 0;
        using var writer = new EventXmlWriter(stream);
        foreach (EventObject eventObject in
                 EventLogEngine.ReadStructured(
                     xmlQuery,
                     cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteXml(eventObject.XMLData);
            count++;
        }
        writer.Complete();
        return count;
    }

    private static long WriteBatch(
        EventLogBatchQuery query,
        EventExportFormat format,
        Stream stream,
        CancellationToken cancellationToken) {

        EventLogBatchQuery effectiveQuery = format == EventExportFormat.Xml
            ? CopyBatchQuery(query, EventReadMode.RawXml)
            : query;
        IEnumerable<EventObject> events =
            EventLogBatchEngine.Read(effectiveQuery, cancellationToken);
        if (format != EventExportFormat.Xml) {
            return WriteProjectedEvents(
                events,
                format,
                stream,
                cancellationToken);
        }

        long count = 0;
        using var writer = new EventXmlWriter(stream);
        foreach (EventObject eventObject in events) {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteXml(eventObject.XMLData);
            count++;
        }
        writer.Complete();
        return count;
    }

    private static long WriteProjectedEvents(
        IEnumerable<EventObject> events,
        EventExportFormat format,
        Stream stream,
        CancellationToken cancellationToken) {

        long count = 0;
        using IEventExportWriter writer = CreateWriter(format, stream);
        foreach (EventObject eventObject in events) {
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(eventObject);
            count++;
        }
        writer.Complete();
        return count;
    }

    private static EventLogChannelQuery CopyChannelQuery(
        EventLogChannelQuery source,
        EventReadMode readMode) {

        return new EventLogChannelQuery(source.LogName) {
            MachineName = source.MachineName,
            Credential = source.Credential,
            Authentication = source.Authentication,
            XPath = source.XPath,
            Oldest = source.Oldest,
            ReadMode = readMode,
            MessageCulture = source.MessageCulture,
            FallbackMessageCulture = source.FallbackMessageCulture,
            MaxEvents = source.MaxEvents,
            BatchSourceIdentity =
                source.BatchSourceIdentity,
            IncludeBookmark = source.IncludeBookmark,
            RemoteConnectionTimeoutMilliseconds =
                source.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = source.RemoteReadTimeoutMilliseconds,
            BufferCapacity = source.BufferCapacity,
            RpcEndpointPort = source.RpcEndpointPort,
            BookmarkXml = source.BookmarkXml,
            BookmarkOffset = source.BookmarkOffset,
            StrictBookmark = source.StrictBookmark
        };
    }

    private static EventLogStructuredQuery CopyStructuredQuery(
        EventLogStructuredQuery source,
        EventReadMode readMode) {

        return new EventLogStructuredQuery(source.QueryXml) {
            SourceKind = source.SourceKind,
            MachineName = source.MachineName,
            Credential = source.Credential,
            Authentication = source.Authentication,
            Oldest = source.Oldest,
            ReadMode = readMode,
            MessageCulture = source.MessageCulture,
            FallbackMessageCulture =
                source.FallbackMessageCulture,
            MaxEvents = source.MaxEvents,
            BatchSourceIdentity =
                source.BatchSourceIdentity,
            IncludeBookmark = source.IncludeBookmark,
            RemoteConnectionTimeoutMilliseconds =
                source.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds =
                source.RemoteReadTimeoutMilliseconds,
            BufferCapacity = source.BufferCapacity,
            RpcEndpointPort = source.RpcEndpointPort,
            BookmarkXml = source.BookmarkXml,
            BookmarkOffset = source.BookmarkOffset,
            StrictBookmark = source.StrictBookmark,
            TolerateQueryErrors = source.TolerateQueryErrors,
            FailureHandler = source.FailureHandler
        };
    }

    private static EventLogFileQuery CopyFileQuery(
        EventLogFileQuery source,
        EventReadMode readMode) {

        return new EventLogFileQuery(source.Path) {
            XPath = source.XPath,
            Oldest = source.Oldest,
            ReadMode = readMode,
            MessageCulture = source.MessageCulture,
            FallbackMessageCulture = source.FallbackMessageCulture,
            MaxEvents = source.MaxEvents,
            BatchSourceIdentity =
                source.BatchSourceIdentity,
            IncludeBookmark = source.IncludeBookmark,
            BookmarkXml = source.BookmarkXml,
            BookmarkOffset = source.BookmarkOffset,
            StrictBookmark = source.StrictBookmark
        };
    }

    internal static EventLogBatchQuery CopyBatchQuery(
        EventLogBatchQuery source,
        EventReadMode readMode) {

        var batches = new List<EventLogBatchQuery>(3);
        if (source.ChannelQueries.Count > 0) {
            batches.Add(EventLogBatchQuery.ForChannels(
                source.ChannelQueries.Select(query =>
                    CopyChannelQuery(query, readMode))));
        }
        if (source.FileQueries.Count > 0) {
            batches.Add(EventLogBatchQuery.ForFiles(
                source.FileQueries.Select(query =>
                    CopyFileQuery(query, readMode))));
        }
        if (source.StructuredQueries.Count > 0) {
            batches.Add(EventLogBatchQuery.ForStructured(
                source.StructuredQueries.Select(query =>
                    CopyStructuredQuery(query, readMode))));
        }
        if (batches.Count == 0) {
            throw new ArgumentException(
                "The batch does not contain any query sources.",
                nameof(source));
        }
        EventLogBatchQuery copy = batches.Count == 1
            ? batches[0]
            : EventLogBatchQuery.Combine(batches);
        copy.MaxEvents = source.MaxEvents;
        copy.MaxConcurrency = source.MaxConcurrency;
        copy.ContinueOnError = source.ContinueOnError;
        copy.FailureHandler = source.FailureHandler;
        return copy;
    }

    private static IEventExportWriter CreateWriter(
        EventExportFormat format,
        Stream stream) {

        return format switch {
            EventExportFormat.Csv => new EventCsvWriter(stream),
            EventExportFormat.JsonLines => new EventJsonLinesWriter(stream),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "Unsupported event export format.")
        };
    }

    private static string ResolveDestination(string outputPath) {
        if (string.IsNullOrWhiteSpace(outputPath)) {
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));
        }
        return Path.GetFullPath(outputPath.Trim().Trim('"', '\''));
    }

    private static void ValidateLocalNativeExportTarget(
        string? machineName) {

        if (EventLogTarget.IsLocalMachine(machineName)) {
            return;
        }

        throw new NotSupportedException(
            "Windows creates native EVTX exports on the computer that owns the remote event-log session, so a local atomic destination cannot be guaranteed. Run the EVTX export on the source computer, or export the remote query to XML, JSON Lines, or CSV.");
    }

    private static void ValidateDestinationDoesNotOverwriteSources(
        string destination,
        IEnumerable<EventLogStructuredQuerySource> sources) {

        foreach (EventLogStructuredQuerySource source in sources) {
            if (source.Kind != EventLogQuerySourceKind.File) {
                continue;
            }
            string sourcePath = Path.GetFullPath(source.Source);
            if (string.Equals(
                    sourcePath,
                    destination,
                    StringComparison.OrdinalIgnoreCase)) {
                throw new IOException(
                    "The export destination cannot overwrite a source event log.");
            }
        }
    }

    internal static string ComputeSha256(
        string path,
        CancellationToken cancellationToken) {

        using SHA256 sha256 = SHA256.Create();
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.SequentialScan);
        var buffer = new byte[1024 * 1024];
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) {
                break;
            }
            sha256.TransformBlock(
                buffer,
                0,
                read,
                null,
                0);
        }
        cancellationToken.ThrowIfCancellationRequested();
        sha256.TransformFinalBlock(
            Array.Empty<byte>(),
            0,
            0);
        return BitConverter.ToString(sha256.Hash!)
            .Replace("-", string.Empty);
    }
}
