using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace EventViewerX.Native;

internal static class WindowsEventArchive {
    internal static void ArchiveFileResources(
        string path,
        int locale,
        CancellationToken cancellationToken) {

        ArchiveFileResources(
            path,
            locale,
            cancellationToken,
            static (archivePath, archiveLocale) => {
                if (!WindowsEventNativeMethods
                        .EvtArchiveExportedLog(
                            IntPtr.Zero,
                            archivePath,
                            archiveLocale,
                            0)) {
                    throw CreateWin32Exception(
                        $"Provider resources could not be archived into '{archivePath}'.");
                }
            });
    }

    internal static void ArchiveFileResources(
        string path,
        int locale,
        CancellationToken cancellationToken,
        Action<string, int> archive,
        Action<string, string, CancellationToken>? copyFile = null) {

        if (archive == null) {
            throw new ArgumentNullException(nameof(archive));
        }
        cancellationToken.ThrowIfCancellationRequested();
        string absolutePath = Path.GetFullPath(
            path.Trim().Trim('"', '\''));
        string directory =
            Path.GetDirectoryName(absolutePath)!;
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(absolutePath)}.{Guid.NewGuid():N}.archive.evtx");
        bool nativeWorkerOwnsTemporaryFile = false;
        try {
            (copyFile ?? CopyFile)(
                absolutePath,
                temporaryPath,
                cancellationToken);
            try {
                _ = BoundedNativeOperation.Execute(
                    () => {
                        try {
                            archive(
                                temporaryPath,
                                locale);
                            return true;
                        } catch {
                            DeleteTemporaryArchive(
                                temporaryPath);
                            throw;
                        }
                    },
                    int.MaxValue,
                    $"Provider resources could not be archived into '{absolutePath}'.",
                    cancellationToken,
                    _ => DeleteTemporaryArchive(
                        temporaryPath),
                    operationAccepted: () =>
                        nativeWorkerOwnsTemporaryFile = true);
                nativeWorkerOwnsTemporaryFile = false;
            } catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch {
                nativeWorkerOwnsTemporaryFile = false;
                throw;
            }
            cancellationToken.ThrowIfCancellationRequested();
            EventLogExporter.PromoteTemporaryFile(
                temporaryPath,
                absolutePath,
                overwrite: true);
        } finally {
            if (!nativeWorkerOwnsTemporaryFile) {
                DeleteTemporaryArchive(
                    temporaryPath);
            }
        }
    }

    private static void DeleteTemporaryArchive(
        string temporaryPath) {

        try {
            if (File.Exists(temporaryPath)) {
                File.Delete(temporaryPath);
            }
        } catch (IOException) {
            // A canceled native archive retains ownership until its worker
            // finishes and invokes this cleanup again.
        } catch (UnauthorizedAccessException) {
            // Preserve the authoritative native archive failure.
        }
    }

    private static void CopyFile(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken) {

        const int bufferSize = 1024 * 1024;
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            FileOptions.SequentialScan);
        using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            FileOptions.SequentialScan);
        var buffer = new byte[bufferSize];
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            int read = source.Read(
                buffer,
                0,
                buffer.Length);
            if (read == 0) {
                break;
            }
            destination.Write(
                buffer,
                0,
                read);
        }
        destination.Flush(
            flushToDisk: true);
    }

    internal static void ExportFile(
        EventLogFileQuery query,
        string targetPath,
        bool archiveResources,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        ValidateNativeExportOptions(
            query.MaxEvents,
            query.BookmarkXml,
            query.BookmarkOffset);
        string sourcePath = Path.GetFullPath(
            query.Path.Trim().Trim('"', '\''));
        Export(
            IntPtr.Zero,
            sourcePath,
            query.XPath,
            targetPath,
            WindowsEventNativeMethods.ExportLogFlags.FilePath,
            archiveResources,
            query.MessageCulture?.LCID ?? 0);
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void ExportChannel(
        EventLogChannelQuery query,
        string targetPath,
        bool archiveResources,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        ValidateNativeExportOptions(
            query.MaxEvents,
            query.BookmarkXml,
            query.BookmarkOffset);
        string machineName = query.MachineName?.Trim() ?? string.Empty;
        bool remote = !EventLogTarget.IsLocalMachine(machineName);
        if (!remote && query.Credential != null) {
            throw new ArgumentException(
                "Credentials can only be used with a remote event log export.",
                nameof(query));
        }

        WindowsEventNativeMethods.EventHandle? session = null;
        try {
            IntPtr sessionHandle = IntPtr.Zero;
            if (remote) {
                session = WindowsEventRemoteSession.Open(
                    machineName,
                    query.Credential,
                    query.Authentication,
                    query.RemoteConnectionTimeoutMilliseconds);
                sessionHandle = session.DangerousGetHandle();
            }
            Export(
                sessionHandle,
                query.LogName,
                query.XPath,
                targetPath,
                WindowsEventNativeMethods.ExportLogFlags.ChannelPath,
                archiveResources,
                query.MessageCulture?.LCID ?? 0);
        } finally {
            session?.Dispose();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void ExportStructured(
        EventLogStructuredQuery query,
        string targetPath,
        bool archiveResources,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        ValidateNativeExportOptions(
            query.MaxEvents,
            query.BookmarkXml,
            query.BookmarkOffset);
        string machineName = query.MachineName?.Trim() ??
                             string.Empty;
        bool remote = !EventLogTarget.IsLocalMachine(machineName);
        if (!remote && query.Credential != null) {
            throw new ArgumentException(
                "Credentials can only be used with a remote event log export.",
                nameof(query));
        }
        IReadOnlyList<EventLogQuerySourceKind> sourceKinds =
            query.ResolveSourceKinds();
        if (sourceKinds.Count != 1) {
            throw new ArgumentException(
                "A native structured EVTX export cannot mix channel and offline-file Query elements.",
                nameof(query));
        }
        EventLogQuerySourceKind sourceKind = sourceKinds[0];
        if (sourceKind == EventLogQuerySourceKind.File && remote) {
            throw new ArgumentException(
                "A file-based structured export must run locally.",
                nameof(query));
        }

        WindowsEventNativeMethods.EventHandle? session = null;
        try {
            IntPtr sessionHandle = IntPtr.Zero;
            if (remote) {
                session = WindowsEventRemoteSession.Open(
                    machineName,
                    query.Credential,
                    query.Authentication,
                    query.RemoteConnectionTimeoutMilliseconds);
                sessionHandle = session.DangerousGetHandle();
            }
            WindowsEventNativeMethods.ExportLogFlags flags =
                sourceKind == EventLogQuerySourceKind.File
                    ? WindowsEventNativeMethods.ExportLogFlags.FilePath
                    : WindowsEventNativeMethods.ExportLogFlags.ChannelPath;
            if (query.TolerateQueryErrors) {
                flags |= WindowsEventNativeMethods.ExportLogFlags
                    .TolerateQueryErrors;
            }
            string? source = sourceKind ==
                             EventLogQuerySourceKind.File
                ? ResolveSingleStructuredFileSource(
                    query.QueryXml)
                : null;
            Export(
                sessionHandle,
                source,
                query.QueryXml,
                targetPath,
                flags,
                archiveResources,
                query.MessageCulture?.LCID ?? 0);
        } finally {
            session?.Dispose();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static string ResolveSingleStructuredFileSource(
        string queryXml) {

        XDocument document;
        try {
            document = XDocument.Parse(
                queryXml,
                LoadOptions.None);
        } catch (Exception exception) when (
            exception is System.Xml.XmlException ||
            exception is ArgumentException) {
            throw new ArgumentException(
                "Structured file export requires valid QueryList XML.",
                nameof(queryXml),
                exception);
        }
        string[] sources = document
            .Descendants()
            .Where(static element =>
                string.Equals(
                    element.Name.LocalName,
                    "Query",
                    StringComparison.OrdinalIgnoreCase))
            .SelectMany(static query => {
                string queryPath =
                    (string?)query.Attribute("Path") ??
                    string.Empty;
                return query
                    .Elements()
                    .Where(static element =>
                        string.Equals(
                            element.Name.LocalName,
                            "Select",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            element.Name.LocalName,
                            "Suppress",
                            StringComparison.OrdinalIgnoreCase))
                    .Select(element =>
                        (string?)element.Attribute("Path") ??
                        queryPath);
            })
            .Where(static path => path.Length > 0)
            .Select(static source => source.StartsWith(
                "file://",
                StringComparison.OrdinalIgnoreCase)
                    ? EventLogStructuredQueryParser.GetFilePath(source)
                    : Path.GetFullPath(source.Trim().Trim('"', '\'')))
            .Distinct(FileSystemPathIdentity.Comparer)
            .ToArray();
        if (sources.Length != 1) {
            throw new NotSupportedException(
                "Native EVTX export can select several channels but cannot merge several offline log files. Use CSV, JSON Lines, or XML for a multi-file QueryList.");
        }
        return sources[0];
    }

    internal static long GetFileRecordCount(string path) {
        return GetFileInformation(path).RecordCount;
    }

    internal static EventLogFileInformation GetFileInformation(
        string path) {

        return GetFileInformation(
            path,
            static absolutePath => {
                using FileStream stream = new(
                    absolutePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite |
                    FileShare.Delete);
            });
    }

    internal static EventLogFileInformation GetFileInformation(
        string path,
        Action<string> validateReadable) {

        if (validateReadable == null) {
            throw new ArgumentNullException(
                nameof(validateReadable));
        }
        string absolutePath = Path.GetFullPath(
            path.Trim().Trim('"', '\''));
        validateReadable(absolutePath);
        using WindowsEventNativeMethods.EventHandle log =
            WindowsEventNativeMethods.EvtOpenLog(
                IntPtr.Zero,
                absolutePath,
                WindowsEventNativeMethods.OpenLogFlags.FilePath);
        if (log.IsInvalid) {
            throw CreateWin32Exception(
                $"Failed to open event log '{absolutePath}'.");
        }

        DateTime creationTime = ReadFileTime(
            log,
            WindowsEventNativeMethods.LogPropertyId.CreationTime,
            absolutePath);
        DateTime lastAccessTime = ReadFileTime(
            log,
            WindowsEventNativeMethods.LogPropertyId.LastAccessTime,
            absolutePath);
        DateTime lastWriteTime = ReadFileTime(
            log,
            WindowsEventNativeMethods.LogPropertyId.LastWriteTime,
            absolutePath);
        long fileSize = ReadInt64(
            log,
            WindowsEventNativeMethods.LogPropertyId.FileSize,
            absolutePath);
        uint attributes = ReadUInt32(
            log,
            WindowsEventNativeMethods.LogPropertyId.Attributes,
            absolutePath);
        long recordCount = ReadInt64(
            log,
            WindowsEventNativeMethods.LogPropertyId.NumberOfLogRecords,
            absolutePath);
        long oldestRecordNumber = ReadInt64(
            log,
            WindowsEventNativeMethods.LogPropertyId.OldestRecordNumber,
            absolutePath);
        bool isFull = ReadBoolean(
            log,
            WindowsEventNativeMethods.LogPropertyId.Full,
            absolutePath);
        return new EventLogFileInformation(
            absolutePath,
            creationTime,
            lastAccessTime,
            lastWriteTime,
            fileSize,
            attributes,
            recordCount,
            oldestRecordNumber,
            isFull);
    }

    private static WindowsEventNativeMethods.EventVariant ReadProperty(
        WindowsEventNativeMethods.EventHandle log,
        WindowsEventNativeMethods.LogPropertyId property,
        string path) {

        int size = Marshal.SizeOf<WindowsEventNativeMethods.EventVariant>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try {
            if (!WindowsEventNativeMethods.EvtGetLogInfo(
                    log,
                    property,
                    size,
                    buffer,
                    out _)) {
                throw CreateWin32Exception(
                    $"Failed to read '{property}' from event log '{path}'.");
            }
            return Marshal.PtrToStructure<
                WindowsEventNativeMethods.EventVariant>(buffer);
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static long ReadInt64(
        WindowsEventNativeMethods.EventHandle log,
        WindowsEventNativeMethods.LogPropertyId property,
        string path) {

        WindowsEventNativeMethods.EventVariant value =
            ReadProperty(log, property, path);
        return value.ScalarType switch {
            WindowsEventNativeMethods.VariantType.UInt64 =>
                checked((long)value.UInt64Value),
            WindowsEventNativeMethods.VariantType.Int64 =>
                value.Int64Value,
            WindowsEventNativeMethods.VariantType.UInt32 =>
                value.UInt32Value,
            WindowsEventNativeMethods.VariantType.Int32 =>
                value.Int32Value,
            _ => throw UnexpectedType(property, value.ScalarType)
        };
    }

    private static uint ReadUInt32(
        WindowsEventNativeMethods.EventHandle log,
        WindowsEventNativeMethods.LogPropertyId property,
        string path) {

        WindowsEventNativeMethods.EventVariant value =
            ReadProperty(log, property, path);
        return value.ScalarType switch {
            WindowsEventNativeMethods.VariantType.UInt32 =>
                value.UInt32Value,
            WindowsEventNativeMethods.VariantType.Int32 =>
                checked((uint)value.Int32Value),
            _ => throw UnexpectedType(property, value.ScalarType)
        };
    }

    private static bool ReadBoolean(
        WindowsEventNativeMethods.EventHandle log,
        WindowsEventNativeMethods.LogPropertyId property,
        string path) {

        WindowsEventNativeMethods.EventVariant value =
            ReadProperty(log, property, path);
        return value.ScalarType switch {
            WindowsEventNativeMethods.VariantType.Boolean =>
                value.Int32Value != 0,
            _ => throw UnexpectedType(property, value.ScalarType)
        };
    }

    private static DateTime ReadFileTime(
        WindowsEventNativeMethods.EventHandle log,
        WindowsEventNativeMethods.LogPropertyId property,
        string path) {

        WindowsEventNativeMethods.EventVariant value =
            ReadProperty(log, property, path);
        long fileTime = value.ScalarType switch {
            WindowsEventNativeMethods.VariantType.FileTime =>
                value.Int64Value,
            WindowsEventNativeMethods.VariantType.UInt64 =>
                unchecked((long)value.UInt64Value),
            WindowsEventNativeMethods.VariantType.Int64 =>
                value.Int64Value,
            _ => throw UnexpectedType(property, value.ScalarType)
        };
        return fileTime <= 0
            ? DateTime.MinValue
            : DateTime.FromFileTimeUtc(fileTime);
    }

    private static InvalidDataException UnexpectedType(
        WindowsEventNativeMethods.LogPropertyId property,
        WindowsEventNativeMethods.VariantType type) {

        return new InvalidDataException(
            $"Windows returned unexpected type '{type}' for log property '{property}'.");
    }

    private static void Export(
        IntPtr session,
        string? source,
        string? xpath,
        string targetPath,
        WindowsEventNativeMethods.ExportLogFlags flags,
        bool archiveResources,
        int locale) {

        string query = string.IsNullOrWhiteSpace(xpath) ? "*" : xpath!;
        if (!WindowsEventNativeMethods.EvtExportLog(
                session,
                source,
                query,
                targetPath,
                flags)) {
            throw CreateWin32Exception(
                $"Failed to export Windows event source '{source ?? "structured query"}' to '{targetPath}'.");
        }
        if (archiveResources &&
            !WindowsEventNativeMethods.EvtArchiveExportedLog(
                session,
                targetPath,
                locale,
                0)) {
            throw CreateWin32Exception(
                $"The event log was exported but its provider resources could not be archived into '{targetPath}'.");
        }
    }

    private static void ValidateNativeExportOptions(
        long maxEvents,
        string? bookmarkXml,
        long bookmarkOffset) {

        if (maxEvents != 0) {
            throw new ArgumentException(
                "Native EVTX export does not support MaxEvents. Put the desired record boundary in XPath.");
        }
        if (!string.IsNullOrWhiteSpace(bookmarkXml) ||
            bookmarkOffset != 1) {
            throw new ArgumentException(
                "Native EVTX export does not support bookmark seek. Put the desired record boundary in XPath.");
        }
    }

    private static Win32Exception CreateWin32Exception(string message) {
        int error = Marshal.GetLastWin32Error();
        return new Win32Exception(error, message);
    }
}
