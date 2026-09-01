using System.Runtime.InteropServices;

namespace EventViewerX.Native;

/// <summary>
/// Presents extended-length filesystem sources to Windows Eventing through a
/// short-lived ordinary path without changing the caller's filesystem or
/// reporting identity.
/// </summary>
internal sealed class WindowsEventFileQueryLease : IDisposable {
    private readonly string? _temporaryDirectory;

    private WindowsEventFileQueryLease(
        NativeEventQuery query,
        string? temporaryDirectory = null) {

        Query = query;
        _temporaryDirectory = temporaryDirectory;
    }

    internal NativeEventQuery Query { get; }

    internal static WindowsEventFileQueryLease Acquire(
        NativeEventQuery query) {

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
            if (!CreateHardLink(nativePath, sourcePath, IntPtr.Zero)) {
                using var source = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var destination = new FileStream(
                    nativePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read);
                source.CopyTo(destination);
            }
            string? structuredQuery = query.Path == null
                ? EventLogStructuredQueryParser.ReplaceFileSources(
                    query.XPath,
                    nativePath)
                : null;
            return new WindowsEventFileQueryLease(
                query.WithFileSource(nativePath, structuredQuery),
                temporaryDirectory);
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

    private static void TryDeleteDirectory(string path) {
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, recursive: true);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
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
