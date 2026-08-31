using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace EventViewerX;

/// <summary>Applies the containing filesystem's path identity rules to canonical full paths.</summary>
internal static class FileSystemPathIdentity {
    private static readonly ConcurrentDictionary<string, bool> CaseSensitivityByDirectory =
        new(StringComparer.Ordinal);

    internal static StringComparer Comparer { get; } = new PathIdentityComparer();

    internal static string GetFullPath(string path) {
        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && uri.IsFile) {
            return Path.GetFullPath(uri.LocalPath);
        }
        return Path.GetFullPath(path);
    }

    internal static string GetIdentity(string path) {
        string fullPath = GetFullPath(path);
        return IsCaseSensitive(fullPath) ? fullPath : fullPath.ToUpperInvariant();
    }

    internal static bool Equals(string left, string right) =>
        Comparer.Equals(GetFullPath(left), GetFullPath(right));

    internal static bool IsCaseSensitive(string path) {
        string directory = FindProbeDirectory(GetFullPath(path));
        return CaseSensitivityByDirectory.GetOrAdd(directory, ProbeCaseSensitivity);
    }

    private static string FindProbeDirectory(string fullPath) {
        string? candidate = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(candidate) && !Directory.Exists(candidate)) {
            candidate = Path.GetDirectoryName(candidate);
        }
        return candidate ?? Path.GetPathRoot(fullPath) ?? fullPath;
    }

    private static bool ProbeCaseSensitivity(string directory) {
        try {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory)) {
                string name = Path.GetFileName(entry);
                string alternateName = ToggleFirstLetterCase(name);
                if (string.Equals(name, alternateName, StringComparison.Ordinal)) {
                    continue;
                }
                bool exactAlternateExists = Directory.EnumerateFileSystemEntries(directory)
                    .Any(candidate => string.Equals(
                        Path.GetFileName(candidate),
                        alternateName,
                        StringComparison.Ordinal));
                if (exactAlternateExists) {
                    return true;
                }
                string alternatePath = Path.Combine(directory, alternateName);
                return !File.Exists(alternatePath) && !Directory.Exists(alternatePath);
            }

            string probeName = ".evx-case-" + Guid.NewGuid().ToString("N") + ".tmp";
            string probePath = Path.Combine(directory, probeName);
            string alternateProbePath = Path.Combine(directory, probeName.ToUpperInvariant());
            using (new FileStream(
                       probePath,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.ReadWrite | FileShare.Delete,
                       1,
                       FileOptions.DeleteOnClose)) {
                return !File.Exists(alternateProbePath);
            }
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
            string? parent = Path.GetDirectoryName(directory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrEmpty(parent) &&
                !string.Equals(parent, directory, StringComparison.Ordinal)) {
                return CaseSensitivityByDirectory.GetOrAdd(parent, ProbeCaseSensitivity);
            }
            return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                   !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }
    }

    private static string ToggleFirstLetterCase(string value) {
        for (int index = 0; index < value.Length; index++) {
            char character = value[index];
            if (!char.IsLetter(character)) {
                continue;
            }
            char toggled = char.IsUpper(character)
                ? char.ToLowerInvariant(character)
                : char.ToUpperInvariant(character);
            return value.Substring(0, index) + toggled + value.Substring(index + 1);
        }
        return value;
    }

    private sealed class PathIdentityComparer : StringComparer {
        public override int Compare(string? left, string? right) =>
            StringComparer.Ordinal.Compare(Normalize(left), Normalize(right));

        public override bool Equals(string? left, string? right) =>
            StringComparer.Ordinal.Equals(Normalize(left), Normalize(right));

        public override int GetHashCode(string value) =>
            StringComparer.Ordinal.GetHashCode(GetIdentity(value));

        private static string? Normalize(string? path) =>
            path == null ? null : GetIdentity(path);
    }
}
