using System.Runtime.InteropServices;

namespace EventViewerX.Providers;

internal static class EventProviderFileRemoval {
    private const uint MoveFileDelayUntilReboot = 0x00000004;

    internal static bool DeleteOrSchedule(
        string directory) {

        if (!Directory.Exists(directory)) {
            return true;
        }
        for (int attempt = 0; attempt < 20; attempt++) {
            try {
                Directory.Delete(directory, recursive: true);
                return true;
            } catch (IOException) {
                Thread.Sleep(100);
            } catch (UnauthorizedAccessException) {
                Thread.Sleep(100);
            }
        }

        string? parent = Path.GetDirectoryName(directory);
        if (!string.IsNullOrWhiteSpace(parent)) {
            string tombstone = Path.Combine(
                parent,
                ".removed-" + Guid.NewGuid().ToString("N"));
            try {
                Directory.Move(directory, tombstone);
                directory = tombstone;
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }

        foreach (string file in Directory
                     .EnumerateFiles(
                         directory,
                         "*",
                         SearchOption.AllDirectories)) {
            if (!MoveFileEx(
                    file,
                    null,
                    MoveFileDelayUntilReboot)) {
                throw new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"Could not remove or schedule provider file '{file}' for deletion.");
            }
        }
        foreach (string childDirectory in Directory
                     .EnumerateDirectories(
                         directory,
                         "*",
                         SearchOption.AllDirectories)
                     .OrderByDescending(
                         static path => path.Length)) {
            if (!MoveFileEx(
                    childDirectory,
                    null,
                    MoveFileDelayUntilReboot)) {
                throw new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"Could not schedule provider directory '{childDirectory}' for deletion.");
            }
        }
        if (!MoveFileEx(
                directory,
                null,
                MoveFileDelayUntilReboot)) {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Could not schedule provider directory '{directory}' for deletion.");
        }
        return false;
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(
        string existingFileName,
        string? newFileName,
        uint flags);
}
