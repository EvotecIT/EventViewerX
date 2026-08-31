using System.Runtime.InteropServices;

namespace EventViewerX;

/// <summary>Applies the host filesystem's path identity rules to canonical full paths.</summary>
internal static class FileSystemPathIdentity {
    internal static bool IsCaseSensitive { get; } =
        !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    internal static StringComparer Comparer { get; } =
        IsCaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

    internal static string GetFullPath(string path) => Path.GetFullPath(path);

    internal static string GetIdentity(string path) {
        string fullPath = GetFullPath(path);
        return IsCaseSensitive ? fullPath : fullPath.ToUpperInvariant();
    }

    internal static bool Equals(string left, string right) =>
        Comparer.Equals(GetFullPath(left), GetFullPath(right));
}
