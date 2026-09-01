using System.Xml.Linq;

namespace EventViewerX;

internal static class EventLogStructuredQueryParser {
    internal static bool IsQueryList(string? queryXml) {
        if (string.IsNullOrWhiteSpace(queryXml)) {
            return false;
        }
        try {
            XDocument document = XDocument.Parse(
                queryXml,
                LoadOptions.PreserveWhitespace);
            return string.Equals(
                document.Root?.Name.LocalName,
                "QueryList",
                StringComparison.Ordinal);
        } catch (System.Xml.XmlException) {
            return false;
        }
    }

    internal static XElement[] ParseQueries(string queryXml) {
        XDocument document = XDocument.Parse(
            queryXml,
            LoadOptions.PreserveWhitespace);
        XElement[] queries = document.Root?
            .Elements()
            .Where(static element =>
                string.Equals(
                    element.Name.LocalName,
                    "Query",
                    StringComparison.Ordinal))
            .Select(static element => new XElement(element))
            .ToArray() ??
            Array.Empty<XElement>();
        if (queries.Length == 0) {
            throw new ArgumentException(
                "A structured query must contain at least one Query element.",
                nameof(queryXml));
        }
        return queries;
    }

    internal static EventLogQuerySourceKind ResolveSourceKind(
        XElement query,
        EventLogQuerySourceKind declaredKind) {

        if (!Enum.IsDefined(
                typeof(EventLogQuerySourceKind),
                declaredKind)) {
            throw new ArgumentOutOfRangeException(
                nameof(declaredKind),
                "The structured query source kind is not supported.");
        }
        string[] paths = GetPaths(query);
        bool hasFile = paths.Any(IsFileSource);
        bool hasChannel = paths.Any(static path =>
            !IsFileSource(path));
        if (hasFile && hasChannel) {
            throw new ArgumentException(
                "One Query element cannot mix channel and offline-file paths because Windows requires one native source kind per query handle.");
        }
        EventLogQuerySourceKind inferredKind = hasFile
            ? EventLogQuerySourceKind.File
            : EventLogQuerySourceKind.Channel;
        if (declaredKind != EventLogQuerySourceKind.Auto &&
            declaredKind != inferredKind) {
            throw new ArgumentException(
                $"Structured query paths are {inferredKind} sources but SourceKind is {declaredKind}.");
        }
        return declaredKind == EventLogQuerySourceKind.Auto
            ? inferredKind
            : declaredKind;
    }

    internal static EventLogQuerySourceKind[] ResolveSourceKinds(
        string queryXml,
        EventLogQuerySourceKind declaredKind) {

        return ParseQueries(queryXml)
            .Select(query =>
                ResolveSourceKind(
                    query,
                    declaredKind))
            .Distinct()
            .ToArray();
    }

    internal static EventLogStructuredQuerySource[] ResolveSources(
        string queryXml,
        EventLogQuerySourceKind declaredKind) {

        var sources = new List<EventLogStructuredQuerySource>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement query in ParseQueries(queryXml)) {
            EventLogQuerySourceKind sourceKind =
                ResolveSourceKind(query, declaredKind);
            foreach (string path in GetPaths(query)) {
                bool isFile = IsFileSource(path);
                if (isFile !=
                    (sourceKind == EventLogQuerySourceKind.File)) {
                    continue;
                }
                string source = isFile
                    ? GetFilePath(path)
                    : path;
                string sourceIdentity = isFile
                    ? FileSystemPathIdentity.GetIdentity(source)
                    : source.ToUpperInvariant();
                string identity = ((int)sourceKind).ToString(
                    System.Globalization.CultureInfo.InvariantCulture) + ":" + sourceIdentity;
                if (identities.Add(identity)) {
                    sources.Add(
                        new EventLogStructuredQuerySource(
                            sourceKind,
                            source));
                }
            }
        }
        return sources.ToArray();
    }

    internal static string AddMinimumRecordIdSuppressions(
        string queryXml,
        EventLogQuerySourceKind declaredKind,
        Func<EventLogStructuredQuerySource, long?> resolver) {

        XDocument document = XDocument.Parse(
            queryXml,
            LoadOptions.PreserveWhitespace);
        XElement[] queries = document.Root?
            .Elements()
            .Where(static element =>
                string.Equals(
                    element.Name.LocalName,
                    "Query",
                    StringComparison.Ordinal))
            .ToArray() ??
            Array.Empty<XElement>();
        if (queries.Length == 0) {
            throw new ArgumentException(
                "A structured query must contain at least one Query element.",
                nameof(queryXml));
        }
        foreach (XElement query in queries) {
            EventLogQuerySourceKind sourceKind =
                ResolveSourceKind(query, declaredKind);
            foreach (string path in GetPaths(query)
                         .Distinct(sourceKind == EventLogQuerySourceKind.File
                             ? FileSystemPathIdentity.Comparer
                             : StringComparer.OrdinalIgnoreCase)) {
                bool isFile = IsFileSource(path);
                if (isFile !=
                    (sourceKind == EventLogQuerySourceKind.File)) {
                    continue;
                }
                string source = isFile
                    ? GetFilePath(path)
                    : path;
                long? minimum = resolver(
                    new EventLogStructuredQuerySource(
                        sourceKind,
                        source));
                if (!minimum.HasValue || minimum.Value <= 0) {
                    continue;
                }
                query.Add(
                    new XElement(
                        query.Name.Namespace + "Suppress",
                        new XAttribute("Path", path),
                        $"*[System[EventRecordID <= {minimum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}]]"));
            }
        }
        return document.ToString(SaveOptions.DisableFormatting);
    }

    internal static int CountIndependentSources(
        string queryXml,
        EventLogQuerySourceKind declaredKind) {

        return ParseQueries(queryXml)
            .Select(query => {
                EventLogQuerySourceKind sourceKind =
                    ResolveSourceKind(
                        query,
                        declaredKind);
                return sourceKind == EventLogQuerySourceKind.File
                    ? "F:" + FileSystemPathIdentity.GetIdentity(GetFilePath(GetFileSourceIdentity(query)))
                    : "C:";
            })
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    internal static string GetFileSourceIdentity(
        XElement query) {

        string[] sources = GetPaths(query)
            .Where(IsFileSource)
            .Distinct(FileSystemPathIdentity.Comparer)
            .ToArray();
        if (sources.Length != 1) {
            throw new ArgumentException(
                "Each offline-file Query element must reference exactly one file path.");
        }
        return sources[0];
    }

    internal static string GetFilePath(
        string fileSourceIdentity) {

        if (!IsFileSource(fileSourceIdentity)) {
            throw new ArgumentException(
                "The structured query source is not a file URI.",
                nameof(fileSourceIdentity));
        }
        if (Uri.TryCreate(
                fileSourceIdentity,
                UriKind.Absolute,
                out Uri? uri) &&
            uri.IsFile) {
            return Path.GetFullPath(uri.LocalPath);
        }
        string path = fileSourceIdentity.Substring(
            "file://".Length);
        if (path.Length >= 3 &&
            path[0] == '/' &&
            char.IsLetter(path[1]) &&
            path[2] == ':') {
            path = path.Substring(1);
        }
        return Path.GetFullPath(
            Uri.UnescapeDataString(path)
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Converts a local offline-event path into an escaped absolute file URI
    /// suitable for QueryList Path attributes.
    /// </summary>
    internal static string CreateFileSourceIdentity(
        string path) {

        string fullPath = FileSystemPathIdentity.GetFullPath(path);
        string escapedPath = fullPath
            .Replace("%", "%25")
            .Replace("#", "%23")
            .Replace("?", "%3F")
            .Replace(
                Path.DirectorySeparatorChar,
                '/');
        if (escapedPath.StartsWith(
                "//",
                StringComparison.Ordinal)) {
            return "file://" +
                   escapedPath.Substring(2);
        }
        return "file://" + escapedPath;
    }

    private static string[] GetPaths(XElement query) {
        string fallbackPath =
            GetPathAttribute(query);
        XElement[] clauses = query
            .Elements()
            .Where(static element =>
                string.Equals(
                    element.Name.LocalName,
                    "Select",
                    StringComparison.Ordinal) ||
                string.Equals(
                    element.Name.LocalName,
                    "Suppress",
                    StringComparison.Ordinal))
            .ToArray();
        if (clauses.Length == 0) {
            return fallbackPath.Length == 0
                ? Array.Empty<string>()
                : new[] { fallbackPath };
        }
        return clauses
            .Select(element => {
                string path = GetPathAttribute(element);
                return path.Length == 0
                    ? fallbackPath
                    : path;
            })
            .Where(static path => path.Length > 0)
            .ToArray();
    }

    private static string GetPathAttribute(XElement element) {
        return element
                   .Attributes()
                   .FirstOrDefault(static attribute =>
                       string.Equals(
                           attribute.Name.LocalName,
                           "Path",
                           StringComparison.Ordinal))
                   ?.Value
                   .Trim() ??
               string.Empty;
    }

    private static bool IsFileSource(string path) {
        return path.StartsWith(
            "file://",
            StringComparison.OrdinalIgnoreCase);
    }
}
