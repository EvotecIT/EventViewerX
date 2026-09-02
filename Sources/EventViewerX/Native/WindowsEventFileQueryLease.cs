using System.Runtime.InteropServices;

namespace EventViewerX.Native;

/// <summary>
/// Presents extended-length filesystem sources to Windows Eventing through a
/// short-lived ordinary path without changing the caller's filesystem or
/// reporting identity.
/// </summary>
internal sealed class WindowsEventFileQueryLease : IDisposable {
    private readonly string? _temporaryDirectory;
    private readonly string? _writeSourcePath;
    private readonly string? _writeNativePath;
    private readonly bool _writeFileRequiresCopyBack;
    private readonly IReadOnlyList<(string DestinationPath, string SourcePath)>
        _linkedFiles;

    private WindowsEventFileQueryLease(
        NativeEventQuery query,
        string? temporaryDirectory = null,
        string? writeSourcePath = null,
        string? writeNativePath = null,
        bool writeFileRequiresCopyBack = false,
        IReadOnlyList<(string DestinationPath, string SourcePath)>? linkedFiles = null) {

        Query = query;
        _temporaryDirectory = temporaryDirectory;
        _writeSourcePath = writeSourcePath;
        _writeNativePath = writeNativePath;
        _writeFileRequiresCopyBack = writeFileRequiresCopyBack;
        _linkedFiles = linkedFiles ??
            Array.Empty<(string DestinationPath, string SourcePath)>();
    }

    internal NativeEventQuery Query { get; }

    internal static WindowsEventFileQueryLease Acquire(
        NativeEventQuery query,
        CancellationToken cancellationToken = default) {

        return Acquire(
            query,
            includeLocaleMetadata: false,
            writable: false,
            cancellationToken);
    }

    internal static WindowsEventFileQueryLease AcquireWithMessageResources(
        NativeEventQuery query,
        CancellationToken cancellationToken = default) {

        return Acquire(
            query,
            includeLocaleMetadata: true,
            writable: false,
            cancellationToken);
    }

    internal static WindowsEventFileQueryLease AcquireForWrite(
        NativeEventQuery query,
        CancellationToken cancellationToken = default) {

        return Acquire(
            query,
            includeLocaleMetadata: false,
            writable: true,
            cancellationToken);
    }

    private static WindowsEventFileQueryLease Acquire(
        NativeEventQuery query,
        bool includeLocaleMetadata,
        bool writable,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        if ((query.Flags & WindowsEventNativeMethods.QueryFlags.FilePath) == 0) {
            return new WindowsEventFileQueryLease(query);
        }
        string? sourcePath = query.Path ?? query.PublisherMetadataPath;
        if (sourcePath == null ||
            string.IsNullOrWhiteSpace(sourcePath) ||
            !FileSystemPathIdentity.IsWindowsExtendedLengthPath(sourcePath)) {
            return new WindowsEventFileQueryLease(query);
        }

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-NativeQuery-" + Guid.NewGuid().ToString("N"));
        string nativePath = Path.Combine(temporaryDirectory, "source.evtx");
        var linkedFiles =
            new List<(string DestinationPath, string SourcePath)>();
        try {
            Directory.CreateDirectory(temporaryDirectory);
            bool linked = CreateLinkOrCopy(
                sourcePath,
                nativePath,
                cancellationToken);
            if (linked) {
                linkedFiles.Add((nativePath, sourcePath));
            }
            if (includeLocaleMetadata) {
                StageLocaleMetadata(
                    sourcePath,
                    temporaryDirectory,
                    linkedFiles,
                    cancellationToken);
            }
            string? structuredQuery = query.Path == null
                ? EventLogStructuredQueryParser.ReplaceFileSources(
                    query.XPath,
                    nativePath)
                : null;
            return new WindowsEventFileQueryLease(
                query.WithFileSource(nativePath, structuredQuery),
                temporaryDirectory,
                writable ? sourcePath : null,
                writable ? nativePath : null,
                writable && !linked,
                linkedFiles);
        } catch {
            TryDeleteDirectory(temporaryDirectory, linkedFiles);
            throw;
        }
    }

    public void Dispose() {
        if (_temporaryDirectory != null) {
            TryDeleteDirectory(_temporaryDirectory, _linkedFiles);
        }
    }

    internal void CommitWrite(
        CancellationToken cancellationToken = default) {

        if (_writeSourcePath == null || _writeNativePath == null) {
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (_writeFileRequiresCopyBack) {
            CopyFileContents(
                _writeNativePath,
                _writeSourcePath,
                overwrite: true,
                cancellationToken);
        }
        CommitLocaleMetadata(
            _writeNativePath,
            _writeSourcePath,
            cancellationToken);
    }

    private static void TryDeleteDirectory(
        string path,
        IReadOnlyList<(string DestinationPath, string SourcePath)> linkedFiles) {

        try {
            foreach ((string destinationPath, string sourcePath) in
                     linkedFiles) {
                DeleteLinkedFilePreservingAttributes(
                    destinationPath,
                    sourcePath);
            }
            if (!Directory.Exists(path)) {
                return;
            }
            foreach (string file in Directory.EnumerateFiles(
                         path,
                         "*",
                         SearchOption.AllDirectories)) {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            foreach (string directory in Directory.EnumerateDirectories(
                         path,
                         "*",
                         SearchOption.AllDirectories)) {
                File.SetAttributes(directory, FileAttributes.Normal);
            }
            Directory.Delete(path, recursive: true);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private static void StageLocaleMetadata(
        string sourcePath,
        string temporaryDirectory,
        ICollection<(string DestinationPath, string SourcePath)> linkedFiles,
        CancellationToken cancellationToken) {

        string? sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrEmpty(sourceDirectory)) {
            return;
        }
        string sourceMetadata = Path.Combine(
            sourceDirectory,
            "LocaleMetaData");
        if (!Directory.Exists(sourceMetadata)) {
            return;
        }

        string destinationMetadata = Path.Combine(
            temporaryDirectory,
            "LocaleMetaData");
        Directory.CreateDirectory(destinationMetadata);
        int sourcePrefixLength = sourceMetadata
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            .Length + 1;
        foreach (string sourceFile in Directory.EnumerateFiles(
                     sourceMetadata,
                     "*",
                     SearchOption.AllDirectories)) {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = sourceFile.Substring(sourcePrefixLength);
            string destinationFile = Path.Combine(
                destinationMetadata,
                relativePath);
            string? destinationDirectory =
                Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrEmpty(destinationDirectory)) {
                Directory.CreateDirectory(destinationDirectory);
            }
            if (CreateLinkOrCopy(
                    sourceFile,
                    destinationFile,
                    cancellationToken)) {
                linkedFiles.Add((destinationFile, sourceFile));
            }
        }
    }

    private static bool CreateLinkOrCopy(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        if (CreateHardLink(destinationPath, sourcePath, IntPtr.Zero)) {
            return true;
        }
        CopyFileContents(
            sourcePath,
            destinationPath,
            overwrite: false,
            cancellationToken);
        return false;
    }

    private static void CopyFileContents(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken) {

        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var destination = new FileStream(
            destinationPath,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        var buffer = new byte[81920];
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            int read = source.Read(buffer, 0, buffer.Length);
            if (read == 0) {
                break;
            }
            destination.Write(buffer, 0, read);
        }
    }

    private static void CommitLocaleMetadata(
        string nativePath,
        string sourcePath,
        CancellationToken cancellationToken) {

        string? nativeDirectory = Path.GetDirectoryName(nativePath);
        string? sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (nativeDirectory == null || sourceDirectory == null) {
            return;
        }
        string nativeMetadata = Path.Combine(
            nativeDirectory,
            "LocaleMetaData");
        if (!Directory.Exists(nativeMetadata)) {
            return;
        }
        string sourceMetadata = Path.Combine(
            sourceDirectory,
            "LocaleMetaData");
        int prefixLength = nativeMetadata
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            .Length + 1;
        foreach (string nativeFile in Directory.EnumerateFiles(
                     nativeMetadata,
                     "*",
                     SearchOption.AllDirectories)) {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = nativeFile.Substring(prefixLength);
            string sourceFile = Path.Combine(sourceMetadata, relativePath);
            string? destinationDirectory = Path.GetDirectoryName(sourceFile);
            if (!string.IsNullOrEmpty(destinationDirectory)) {
                Directory.CreateDirectory(destinationDirectory);
            }
            CopyFileContents(
                nativeFile,
                sourceFile,
                overwrite: true,
                cancellationToken);
        }
    }

    private static void DeleteLinkedFilePreservingAttributes(
        string destinationPath,
        string sourcePath) {

        if (!File.Exists(destinationPath)) {
            return;
        }
        FileAttributes attributes = File.GetAttributes(destinationPath);
        if ((attributes & FileAttributes.ReadOnly) == 0) {
            File.Delete(destinationPath);
            return;
        }
        File.SetAttributes(
            destinationPath,
            attributes & ~FileAttributes.ReadOnly);
        try {
            File.Delete(destinationPath);
        } finally {
            if (File.Exists(sourcePath)) {
                File.SetAttributes(sourcePath, attributes);
            }
        }
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true,
        EntryPoint = "CreateHardLinkW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string fileName,
        string existingFileName,
        IntPtr securityAttributes);
}
