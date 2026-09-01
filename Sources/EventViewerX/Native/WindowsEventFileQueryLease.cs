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

    private WindowsEventFileQueryLease(
        NativeEventQuery query,
        string? temporaryDirectory = null,
        string? writeSourcePath = null,
        string? writeNativePath = null) {

        Query = query;
        _temporaryDirectory = temporaryDirectory;
        _writeSourcePath = writeSourcePath;
        _writeNativePath = writeNativePath;
    }

    internal NativeEventQuery Query { get; }

    internal static WindowsEventFileQueryLease Acquire(
        NativeEventQuery query) {

        return Acquire(
            query,
            includeLocaleMetadata: false,
            writable: false);
    }

    internal static WindowsEventFileQueryLease AcquireWithMessageResources(
        NativeEventQuery query) {

        return Acquire(
            query,
            includeLocaleMetadata: true,
            writable: false);
    }

    internal static WindowsEventFileQueryLease AcquireForWrite(
        NativeEventQuery query) {

        return Acquire(
            query,
            includeLocaleMetadata: false,
            writable: true);
    }

    private static WindowsEventFileQueryLease Acquire(
        NativeEventQuery query,
        bool includeLocaleMetadata,
        bool writable) {

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
        try {
            Directory.CreateDirectory(temporaryDirectory);
            bool linked = CreateLinkOrCopy(sourcePath, nativePath);
            if (includeLocaleMetadata) {
                StageLocaleMetadata(sourcePath, temporaryDirectory);
            }
            string? structuredQuery = query.Path == null
                ? EventLogStructuredQueryParser.ReplaceFileSources(
                    query.XPath,
                    nativePath)
                : null;
            return new WindowsEventFileQueryLease(
                query.WithFileSource(nativePath, structuredQuery),
                temporaryDirectory,
                writable && !linked ? sourcePath : null,
                writable && !linked ? nativePath : null);
        } catch {
            TryDeleteDirectory(temporaryDirectory);
            throw;
        }
    }

    public void Dispose() {
        if (_temporaryDirectory != null) {
            TryDeleteDirectory(_temporaryDirectory);
        }
    }

    internal void CommitWrite() {
        if (_writeSourcePath == null || _writeNativePath == null) {
            return;
        }
        using var source = new FileStream(
            _writeNativePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var destination = new FileStream(
            _writeSourcePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        source.CopyTo(destination);
    }

    private static void TryDeleteDirectory(string path) {
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, recursive: true);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private static void StageLocaleMetadata(
        string sourcePath,
        string temporaryDirectory) {

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
            string relativePath = sourceFile.Substring(sourcePrefixLength);
            string destinationFile = Path.Combine(
                destinationMetadata,
                relativePath);
            string? destinationDirectory =
                Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrEmpty(destinationDirectory)) {
                Directory.CreateDirectory(destinationDirectory);
            }
            _ = CreateLinkOrCopy(sourceFile, destinationFile);
        }
    }

    private static bool CreateLinkOrCopy(
        string sourcePath,
        string destinationPath) {

        if (CreateHardLink(destinationPath, sourcePath, IntPtr.Zero)) {
            return true;
        }
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        source.CopyTo(destination);
        return false;
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
