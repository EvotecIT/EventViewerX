using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml.Linq;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Dependency-free streaming engine for Windows event sources.
/// </summary>
public static partial class EventLogEngine {
    /// <summary>
    /// Streams records from a local or remote Windows event channel using the owned native engine.
    /// </summary>
    /// <param name="query">Channel, remote target, projection, and culture options.</param>
    /// <param name="cancellationToken">Token used to stop enumeration between native batches and records.</param>
    /// <returns>A lazy stream of fully detached event snapshots.</returns>
    public static IEnumerable<EventObject> ReadChannel(
        EventLogChannelQuery query,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        query = EventLogQuerySnapshot.Copy(query);
        EventReadModeValidation.EnsureDefined(
            query.ReadMode,
            nameof(query));
        if (query.MaxEvents < 0) {
            throw new ArgumentOutOfRangeException(nameof(query), "Maximum events must be greater than or equal to zero.");
        }
        if (query.RemoteConnectionTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Remote connection timeout must be greater than zero.");
        }
        if (query.RemoteReadTimeoutMilliseconds < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Remote read timeout must be greater than or equal to zero.");
        }
        if (query.BufferCapacity <= 0 || query.BufferCapacity > 4096) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Buffer capacity must be between 1 and 4096.");
        }
        if (query.RpcEndpointPort <= 0 || query.RpcEndpointPort > 65535) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "RPC endpoint port must be between 1 and 65535.");
        }

        string machineName = query.MachineName?.Trim() ?? string.Empty;
        bool remote = !EventLogTarget.IsLocalMachine(machineName);
        if (!remote && query.Credential != null) {
            throw new ArgumentException(
                "Credentials can only be used with a remote event log query.",
                nameof(query));
        }
        if (!Enum.IsDefined(typeof(EventLogAuthentication), query.Authentication)) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The remote authentication value is not supported.");
        }
        string logName = query.LogName;
        string xpath = string.IsNullOrWhiteSpace(query.XPath) ? "*" : query.XPath;
        ForwardedEventsQuerySafety.EnsureNativeChannelQueryIsSafe(
            logName,
            xpath);
        bool oldest = query.Oldest;
        EventReadMode readMode = query.ReadMode;
        int messageLocale = query.MessageCulture?.LCID ?? 0;
        int fallbackMessageLocale =
            query.FallbackMessageCulture?.LCID ?? 0;
        bool includeBookmark = query.IncludeBookmark;
        long maxEvents = query.MaxEvents;
        int remoteConnectionTimeoutMilliseconds =
            query.RemoteConnectionTimeoutMilliseconds;
        int remoteReadTimeoutMilliseconds = query.RemoteReadTimeoutMilliseconds;
        int bufferCapacity = query.BufferCapacity;
        int rpcEndpointPort = query.RpcEndpointPort;
        NetworkCredential? credential = query.Credential;
        EventLogAuthentication authentication = query.Authentication;
        string? bookmarkXml = string.IsNullOrWhiteSpace(query.BookmarkXml)
            ? null
            : query.BookmarkXml;
        long bookmarkOffset = query.BookmarkOffset;
        bool strictBookmark = query.StrictBookmark;
        DateTime? managedStartTimeUtc = query.ManagedStartTimeUtc;
        DateTime? managedEndTimeUtc = query.ManagedEndTimeUtc;
        Func<EventObject, bool>? managedPredicate = query.ManagedPredicate;
        long managedMaxEventsScanned = query.ManagedMaxEventsScanned;
        Action? managedScanLimitReached = query.ManagedScanLimitReached;
        if (managedMaxEventsScanned < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Maximum managed scanned events must be greater than or equal to zero.");
        }

        return ReadChannelIterator(
            remote,
            machineName,
            logName,
            xpath,
            oldest,
            readMode,
            messageLocale,
            fallbackMessageLocale,
            includeBookmark,
            maxEvents,
            remoteConnectionTimeoutMilliseconds,
            remoteReadTimeoutMilliseconds,
            bufferCapacity,
            rpcEndpointPort,
            credential,
            authentication,
            bookmarkXml,
            bookmarkOffset,
            strictBookmark,
            managedStartTimeUtc,
            managedEndTimeUtc,
            managedPredicate,
            managedMaxEventsScanned,
            managedScanLimitReached,
            cancellationToken);
    }

    /// <summary>
    /// Streams records selected by Windows Event Log QueryList XML, including multi-channel select/suppress queries.
    /// </summary>
    public static IEnumerable<EventObject> ReadStructured(
        EventLogStructuredQuery query,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        query = EventLogQuerySnapshot.Copy(query);
        EventReadModeValidation.EnsureDefined(
            query.ReadMode,
            nameof(query));
        ValidateRemoteOptions(
            query.MaxEvents,
            query.RemoteConnectionTimeoutMilliseconds,
            query.RemoteReadTimeoutMilliseconds,
            query.BufferCapacity,
            query.RpcEndpointPort,
            query.Authentication,
            nameof(query));

        string machineName = query.MachineName?.Trim() ?? string.Empty;
        bool remote = !EventLogTarget.IsLocalMachine(machineName);
        if (!remote && query.Credential != null) {
            throw new ArgumentException(
                "Credentials can only be used with a remote event log query.",
                nameof(query));
        }
        XElement[] queryElements =
            EventLogStructuredQueryParser.ParseQueries(
                query.QueryXml);
        ForwardedEventsQuerySafety.EnsureNativeStructuredQueryIsSafe(
            queryElements);
        var resolvedSources = queryElements
            .Select(queryElement => new {
                Query = queryElement,
                Kind =
                    EventLogStructuredQueryParser.ResolveSourceKind(
                        queryElement,
                        query.SourceKind)
            })
            .ToArray();
        EventLogQuerySourceKind[] sourceKinds = resolvedSources
            .Select(static source => source.Kind)
            .Distinct()
            .ToArray();
        string[] fileSources = resolvedSources
            .Where(static source =>
                source.Kind == EventLogQuerySourceKind.File)
            .Select(static source =>
                EventLogStructuredQueryParser.GetFilePath(
                    EventLogStructuredQueryParser.GetFileSourceIdentity(source.Query)))
            .Distinct(FileSystemPathIdentity.Comparer)
            .ToArray();
        if (!string.IsNullOrWhiteSpace(
                query.BookmarkXml) &&
            query.GetIndependentSourceCount() > 1) {
            throw new ArgumentException(
                "A native bookmark can only resume one independent structured-query source.",
                nameof(query));
        }
        if (sourceKinds.Length > 1 ||
            fileSources.Length > 1) {
            var splitBatch =
                EventLogBatchQuery.ForStructured(
                    new[] { query });
            splitBatch.MaxEvents = query.MaxEvents;
            return EventLogBatchEngine.Read(
                EventLogBatchConsolidator.Consolidate(
                    splitBatch),
                cancellationToken);
        }
        EventLogQuerySourceKind sourceKind = sourceKinds[0];
        if (sourceKind == EventLogQuerySourceKind.File &&
            (remote || query.Credential != null)) {
            throw new ArgumentException(
                "Offline event log files are read locally and cannot use a remote machine or credentials.",
                nameof(query));
        }
        WindowsEventNativeMethods.QueryFlags flags =
            (sourceKind == EventLogQuerySourceKind.File
                ? WindowsEventNativeMethods.QueryFlags.FilePath
                : WindowsEventNativeMethods.QueryFlags.ChannelPath) |
            (query.Oldest
                ? WindowsEventNativeMethods.QueryFlags.ForwardDirection
                : WindowsEventNativeMethods.QueryFlags.ReverseDirection);
        if (query.TolerateQueryErrors) {
            flags |= WindowsEventNativeMethods.QueryFlags.TolerateQueryErrors;
        }
        string? filePath = sourceKind == EventLogQuerySourceKind.File
            ? fileSources[0]
            : null;

        return ReadSourceIterator(
            remote,
            filePath ?? machineName,
            path: null,
            publisherMetadataPath: filePath,
            query.QueryXml,
            displayName: filePath ??
                "structured event query",
            containerLog: filePath ??
                string.Empty,
            flags,
            query.ReadMode,
            query.MessageCulture?.LCID ?? 0,
            query.FallbackMessageCulture?.LCID ?? 0,
            query.IncludeBookmark,
            query.MaxEvents,
            query.RemoteConnectionTimeoutMilliseconds,
            query.RemoteReadTimeoutMilliseconds,
            query.BufferCapacity,
            query.RpcEndpointPort,
            query.Credential,
            query.Authentication,
            string.IsNullOrWhiteSpace(query.BookmarkXml)
                ? null
                : query.BookmarkXml,
            query.BookmarkOffset,
            query.StrictBookmark,
            query.ManagedStartTimeUtc,
            query.ManagedEndTimeUtc,
            managedPredicate: null,
            managedMaxEventsScanned: 0,
            managedScanLimitReached: null,
            query.FailureHandler,
            cancellationToken);
    }

    /// <summary>
    /// Streams records from an offline Windows event log using the owned native projection engine.
    /// </summary>
    /// <param name="query">File query and projection options.</param>
    /// <param name="cancellationToken">Token used to stop enumeration between native batches and records.</param>
    /// <returns>A lazy stream of fully detached event snapshots.</returns>
    public static IEnumerable<EventObject> ReadFile(
        EventLogFileQuery query,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (query.SavedEventReader != null) {
            return ReadSavedFileIterator(EventLogQuerySnapshot.Copy(query), cancellationToken);
        }
        NativeEventQuery nativeQuery = CreateNativeFileQuery(
            query,
            out string path,
            out long maxEvents,
            out EventReadMode readMode);
        return ReadFileIterator(
            nativeQuery,
            path,
            maxEvents,
            readMode,
            cancellationToken);
    }

    private static IEnumerable<EventObject> ReadSavedFileIterator(
        EventLogFileQuery query,
        CancellationToken cancellationToken) {

        EventReadModeValidation.EnsureDefined(query.ReadMode, nameof(query));
        if (query.MaxEvents < 0) {
            throw new ArgumentOutOfRangeException(nameof(query), "Maximum events must be greater than or equal to zero.");
        }
        if (query.IncludeBookmark || !string.IsNullOrWhiteSpace(query.BookmarkXml)) {
            throw new NotSupportedException(
                "The configured saved-event reader does not provide Windows bookmark semantics. " +
                "Use record identifiers for portable checkpoints or omit SavedEventReader to use the Windows Eventing API.");
        }
        string path = FileSystemPathIdentity.GetFullPath(
            query.Path.Trim().Trim('"', '\''));
        EnsureFileReadable(path);
        long returned = 0;
        foreach (SavedEventRecord record in query.SavedEventReader!.Read(
                     query,
                     query.SavedEventDiagnosticHandler,
                     cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (record == null) {
                throw new InvalidDataException("A saved-event reader returned a null record.");
            }
            yield return record.ToEventObject(path, query.ReadMode);
            returned++;
            if (query.MaxEvents > 0 && returned >= query.MaxEvents) {
                yield break;
            }
        }
    }

    internal static IEnumerable<string> ReadFileXml(
        EventLogFileQuery query,
        CancellationToken cancellationToken) {

        NativeEventQuery nativeQuery = CreateNativeFileQuery(
            query,
            out _,
            out long maxEvents,
            out _);
        return ReadFileXmlIterator(nativeQuery, maxEvents, cancellationToken);
    }

    internal static long CopyFileXml(
        EventLogFileQuery query,
        Stream destination,
        CancellationToken cancellationToken) {

        NativeEventQuery nativeQuery = CreateNativeFileQuery(
            query,
            out _,
            out long maxEvents,
            out _);
        return WindowsEventReader.CopyXml(
            nativeQuery,
            destination,
            maxEvents,
            cancellationToken);
    }

    private static IEnumerable<EventObject> ReadFileIterator(
        NativeEventQuery nativeQuery,
        string path,
        long maxEvents,
        EventReadMode readMode,
        CancellationToken cancellationToken) {

        long returned = 0;
        foreach (EventObject eventObject in WindowsEventReader.Read(
                     nativeQuery,
                     readMode,
                     path,
                     path,
                     cancellationToken)) {
            eventObject.QuerySourceKind = EventLogQuerySourceKind.File;
            yield return eventObject;
            returned++;
            if (maxEvents > 0 && returned >= maxEvents) {
                yield break;
            }
        }
    }

    private static IEnumerable<string> ReadFileXmlIterator(
        NativeEventQuery nativeQuery,
        long maxEvents,
        CancellationToken cancellationToken) {

        long returned = 0;
        foreach (string xml in WindowsEventReader.ReadXml(
                     nativeQuery,
                     cancellationToken)) {
            yield return xml;
            returned++;
            if (maxEvents > 0 && returned >= maxEvents) {
                yield break;
            }
        }
    }

    private static NativeEventQuery CreateNativeFileQuery(
        EventLogFileQuery query,
        out string path,
        out long maxEvents,
        out EventReadMode readMode) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventReadModeValidation.EnsureDefined(
            query.ReadMode,
            nameof(query));
        if (query.MaxEvents < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Maximum events must be greater than or equal to zero.");
        }

        path = FileSystemPathIdentity.GetFullPath(
            query.Path.Trim().Trim('"', '\''));
        EnsureFileReadable(path);
        string xpath = string.IsNullOrWhiteSpace(query.XPath) ? "*" : query.XPath;
        WindowsEventNativeMethods.QueryFlags flags =
            WindowsEventNativeMethods.QueryFlags.FilePath |
            (query.Oldest
                ? WindowsEventNativeMethods.QueryFlags.ForwardDirection
                : WindowsEventNativeMethods.QueryFlags.ReverseDirection);
        maxEvents = query.MaxEvents;
        readMode = query.ReadMode;
        return new NativeEventQuery(
            IntPtr.Zero,
            path,
            xpath,
            flags,
            path,
            path,
            query.MessageCulture?.LCID ?? 0,
            query.FallbackMessageCulture?.LCID ?? 0,
            includeBookmark: query.IncludeBookmark,
            bookmarkXml: string.IsNullOrWhiteSpace(query.BookmarkXml)
                ? null
                : query.BookmarkXml,
            bookmarkOffset: query.BookmarkOffset,
            strictBookmark: query.StrictBookmark);
    }

    /// <summary>
    /// Verifies that an EVTX source can actually be opened for reading so
    /// missing and access-denied paths retain their distinct exception types.
    /// </summary>
    internal static void EnsureFileReadable(string path) {
        try {
            EnsureFileReadable(
                path,
                static filePath => new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete));
        } catch (DirectoryNotFoundException exception) {
            throw new FileNotFoundException(
                $"The event log file '{path}' does not exist.",
                path,
                exception);
        }
    }

    /// <summary>
    /// Verifies EVTX read access through an injectable filesystem boundary.
    /// </summary>
    internal static void EnsureFileReadable(
        string path,
        Func<string, Stream> openFile) {

        using Stream stream = openFile(path);
    }

    private static IEnumerable<EventObject> ReadChannelIterator(
        bool remote,
        string machineName,
        string logName,
        string xpath,
        bool oldest,
        EventReadMode readMode,
        int messageLocale,
        int fallbackMessageLocale,
        bool includeBookmark,
        long maxEvents,
        int remoteConnectionTimeoutMilliseconds,
        int remoteReadTimeoutMilliseconds,
        int bufferCapacity,
        int rpcEndpointPort,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        string? bookmarkXml,
        long bookmarkOffset,
        bool strictBookmark,
        DateTime? managedStartTimeUtc,
        DateTime? managedEndTimeUtc,
        Func<EventObject, bool>? managedPredicate,
        long managedMaxEventsScanned,
        Action? managedScanLimitReached,
        CancellationToken cancellationToken) {

        WindowsEventNativeMethods.QueryFlags flags =
            WindowsEventNativeMethods.QueryFlags.ChannelPath |
            (oldest
                ? WindowsEventNativeMethods.QueryFlags.ForwardDirection
                : WindowsEventNativeMethods.QueryFlags.ReverseDirection);
        return ReadSourceIterator(
            remote,
            machineName,
            logName,
            publisherMetadataPath: null,
            xpath,
            logName,
            logName,
            flags,
            readMode,
            messageLocale,
            fallbackMessageLocale,
            includeBookmark,
            maxEvents,
            remoteConnectionTimeoutMilliseconds,
            remoteReadTimeoutMilliseconds,
            bufferCapacity,
            rpcEndpointPort,
            credential,
            authentication,
            bookmarkXml,
            bookmarkOffset,
            strictBookmark,
            managedStartTimeUtc,
            managedEndTimeUtc,
            managedPredicate,
            managedMaxEventsScanned,
            managedScanLimitReached,
            failureHandler: null,
            cancellationToken);
    }

    private static IEnumerable<EventObject> ReadSourceIterator(
        bool remote,
        string machineName,
        string? path,
        string? publisherMetadataPath,
        string query,
        string displayName,
        string containerLog,
        WindowsEventNativeMethods.QueryFlags flags,
        EventReadMode readMode,
        int messageLocale,
        int fallbackMessageLocale,
        bool includeBookmark,
        long maxEvents,
        int remoteConnectionTimeoutMilliseconds,
        int remoteReadTimeoutMilliseconds,
        int bufferCapacity,
        int rpcEndpointPort,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        string? bookmarkXml,
        long bookmarkOffset,
        bool strictBookmark,
        DateTime? managedStartTimeUtc,
        DateTime? managedEndTimeUtc,
        Func<EventObject, bool>? managedPredicate,
        long managedMaxEventsScanned,
        Action? managedScanLimitReached,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken cancellationToken) {

        bool hasManagedTimeWindow =
            managedStartTimeUtc.HasValue || managedEndTimeUtc.HasValue;
        bool hasManagedSelection =
            hasManagedTimeWindow || managedPredicate != null;
        long nativeMaxEvents = hasManagedSelection ? 0 : maxEvents;
        IEnumerable<EventObject> events;
        if (remote) {
            events = WindowsEventRemoteReader.Read(
                machineName,
                path,
                query,
                displayName,
                containerLog,
                flags,
                readMode,
                messageLocale,
                fallbackMessageLocale,
                includeBookmark,
                nativeMaxEvents,
                remoteConnectionTimeoutMilliseconds,
                remoteReadTimeoutMilliseconds,
                bufferCapacity,
                rpcEndpointPort,
                credential,
                authentication,
                bookmarkXml,
                bookmarkOffset,
                strictBookmark,
                failureHandler,
                cancellationToken);
        } else {
            var nativeQuery = new NativeEventQuery(
                IntPtr.Zero,
                path,
                query,
                flags,
                displayName,
                publisherMetadataPath:
                    publisherMetadataPath,
                messageLocale: messageLocale,
                fallbackMessageLocale: fallbackMessageLocale,
                includeBookmark: includeBookmark,
                bookmarkXml: bookmarkXml,
                bookmarkOffset: bookmarkOffset,
                strictBookmark: strictBookmark,
                machineName: machineName,
                failureHandler: failureHandler);
            events = WindowsEventReader.Read(
                nativeQuery,
                readMode,
                (flags & WindowsEventNativeMethods.QueryFlags.FilePath) != 0
                    ? machineName
                    : Environment.MachineName,
                containerLog,
                cancellationToken);
        }

        long returned = 0;
        long scanned = 0;
        bool oldest = (flags & WindowsEventNativeMethods.QueryFlags.ForwardDirection) != 0;
        foreach (EventObject eventObject in events) {
            eventObject.QuerySourceKind = (flags & WindowsEventNativeMethods.QueryFlags.FilePath) != 0
                ? EventLogQuerySourceKind.File
                : EventLogQuerySourceKind.Channel;
            if (managedMaxEventsScanned > 0 &&
                scanned >= managedMaxEventsScanned) {
                managedScanLimitReached?.Invoke();
                yield break;
            }
            scanned++;
            if (hasManagedTimeWindow) {
                if (ForwardedEventsQuerySafety.HasCrossedWindow(
                        eventObject,
                        oldest,
                        managedStartTimeUtc,
                        managedEndTimeUtc)) {
                    yield break;
                }
                if (!ForwardedEventsQuerySafety.ShouldInclude(
                        eventObject,
                        managedStartTimeUtc,
                        managedEndTimeUtc)) {
                    continue;
                }
            }
            if (managedPredicate != null &&
                !managedPredicate(eventObject)) {
                continue;
            }
            yield return eventObject;
            returned++;
            if (maxEvents > 0 && returned >= maxEvents) {
                yield break;
            }
        }
    }

    private static void ValidateRemoteOptions(
        long maxEvents,
        int remoteConnectionTimeoutMilliseconds,
        int remoteReadTimeoutMilliseconds,
        int bufferCapacity,
        int rpcEndpointPort,
        EventLogAuthentication authentication,
        string parameterName) {

        if (maxEvents < 0) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Maximum events must be greater than or equal to zero.");
        }
        if (remoteConnectionTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Remote connection timeout must be greater than zero.");
        }
        if (remoteReadTimeoutMilliseconds < 0) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Remote read timeout must be greater than or equal to zero.");
        }
        if (bufferCapacity <= 0 || bufferCapacity > 4096) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Buffer capacity must be between 1 and 4096.");
        }
        if (rpcEndpointPort <= 0 || rpcEndpointPort > 65535) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "RPC endpoint port must be between 1 and 65535.");
        }
        if (!Enum.IsDefined(typeof(EventLogAuthentication), authentication)) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The remote authentication value is not supported.");
        }
    }
}
