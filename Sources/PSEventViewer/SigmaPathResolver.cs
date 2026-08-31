namespace PSEventViewer;

internal static class SigmaPathResolver {
    internal static IReadOnlyList<string> Resolve(
        SessionState sessionState,
        IEnumerable<string> patterns,
        string parameterName) {

        var paths = new HashSet<string>(FileSystemPathIdentity.Comparer);
        foreach (string pattern in patterns) {
            try {
                foreach (string path in sessionState.Path.GetResolvedProviderPathFromPSPath(
                             pattern,
                             out ProviderInfo provider)) {
                    if (!string.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase)) {
                        throw new PSArgumentException(
                            $"Sigma path '{pattern}' must use the FileSystem provider.",
                            parameterName);
                    }
                    paths.Add(System.IO.Path.GetFullPath(path));
                }
            } catch (ItemNotFoundException) {
                throw new PSArgumentException(
                    $"No Sigma rule files match path '{pattern}'.",
                    parameterName);
            }
        }
        if (paths.Count == 0) {
            throw new PSArgumentException("At least one Sigma rule file is required.", parameterName);
        }
        return paths.OrderBy(static path => path, FileSystemPathIdentity.Comparer).ToArray();
    }
}
