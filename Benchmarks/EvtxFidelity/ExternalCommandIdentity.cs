using System.Diagnostics;
using System.Security.Cryptography;

internal sealed record ExternalCommandIdentity(
    string SuppliedCommand,
    string? ResolvedPath,
    long? FileBytes,
    string? FileSha256,
    string? FileVersion) {

    internal static ExternalCommandIdentity? Create(string? command) {
        if (string.IsNullOrWhiteSpace(command)) {
            return null;
        }

        string? resolvedPath = Resolve(command);
        if (resolvedPath == null) {
            return new ExternalCommandIdentity(command, null, null, null, null);
        }

        var file = new FileInfo(resolvedPath);
        string? fileVersion = null;
        try {
            fileVersion = FileVersionInfo.GetVersionInfo(resolvedPath).FileVersion;
        } catch (Exception exception) when (
            exception is FileNotFoundException or IOException or UnauthorizedAccessException) {
            // The SHA-256 and resolved path remain sufficient provenance for non-PE executables.
        }
        return new ExternalCommandIdentity(
            command,
            resolvedPath,
            file.Length,
            ComputeSha256(resolvedPath),
            fileVersion);
    }

    private static string? Resolve(string command) {
        if (File.Exists(command)) {
            return Path.GetFullPath(command);
        }
        if (Path.IsPathRooted(command) ||
            command.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            command.IndexOf(Path.AltDirectorySeparatorChar) >= 0) {
            return null;
        }

        string[] extensions = OperatingSystem.IsWindows() && string.IsNullOrEmpty(Path.GetExtension(command))
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { string.Empty };
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            foreach (string extension in extensions) {
                string candidate = Path.Combine(directory, command + extension);
                if (File.Exists(candidate)) {
                    return Path.GetFullPath(candidate);
                }
            }
        }
        return null;
    }

    private static string ComputeSha256(string path) {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
