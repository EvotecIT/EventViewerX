using System.Management.Automation;

namespace PSEventViewer;

/// <summary>
/// Expands PowerShell wildcard syntax without removing an extended-length
/// prefix needed by the Windows filesystem.
/// </summary>
internal static class PowerShellExtendedPathExpander {
    internal static IEnumerable<string> Expand(string pattern) {
        string root = Path.GetPathRoot(pattern) ?? string.Empty;
        string remainder = pattern.Substring(root.Length);
        string[] segments = remainder.Split(
            new[] {
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            },
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) {
            yield break;
        }

        IReadOnlyList<string> directories = new[] { root };
        for (int index = 0; index < segments.Length - 1; index++) {
            string segment = segments[index];
            var wildcard = new WildcardPattern(
                segment,
                WildcardOptions.IgnoreCase |
                WildcardOptions.CultureInvariant);
            bool containsWildcard =
                WildcardPattern.ContainsWildcardCharacters(segment);
            var next = new List<string>();
            foreach (string directory in directories) {
                if (containsWildcard) {
                    foreach (string candidate in Directory.EnumerateDirectories(
                                 directory,
                                 "*",
                                 SearchOption.TopDirectoryOnly)) {
                        if (wildcard.IsMatch(Path.GetFileName(candidate))) {
                            next.Add(candidate);
                        }
                    }
                } else {
                    string candidate = FileSystemPathIdentity.GetFullPath(
                        Path.Combine(directory, segment));
                    if (Directory.Exists(candidate)) {
                        next.Add(candidate);
                    }
                }
            }
            directories = next;
        }

        string fileSegment = segments[segments.Length - 1];
        var fileWildcard = new WildcardPattern(
            fileSegment,
            WildcardOptions.IgnoreCase |
            WildcardOptions.CultureInvariant);
        foreach (string directory in directories) {
            if (!Directory.Exists(directory)) {
                continue;
            }
            foreach (string file in Directory.EnumerateFiles(
                         directory,
                         "*",
                         SearchOption.TopDirectoryOnly)) {
                if (fileWildcard.IsMatch(Path.GetFileName(file))) {
                    yield return FileSystemPathIdentity.GetFullPath(file);
                }
            }
        }
    }

}
