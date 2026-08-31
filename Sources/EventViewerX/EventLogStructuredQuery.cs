using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace EventViewerX;

/// <summary>
/// Defines a native structured XML query that can select or suppress events across several channels.
/// </summary>
public sealed class EventLogStructuredQuery {
    /// <summary>Creates a structured query over one or more channels using a raw native XPath.</summary>
    public static EventLogStructuredQuery ForChannels(
        IEnumerable<string> logNames,
        string xpath = "*") {

        return new EventLogStructuredQuery(
            BuildQueryXml(logNames, xpath, filePaths: false)) {
            SourceKind = EventLogQuerySourceKind.Channel
        };
    }

    /// <summary>Creates a structured query over one or more offline event-log files using a raw native XPath.</summary>
    public static EventLogStructuredQuery ForFiles(
        IEnumerable<string> paths,
        string xpath = "*") {

        return new EventLogStructuredQuery(
            BuildQueryXml(paths, xpath, filePaths: true)) {
            SourceKind = EventLogQuerySourceKind.File
        };
    }

    /// <summary>Creates a structured XML query.</summary>
    public EventLogStructuredQuery(string queryXml) {
        if (string.IsNullOrWhiteSpace(queryXml)) {
            throw new ArgumentException(
                "Structured query XML cannot be null or empty.",
                nameof(queryXml));
        }
        QueryXml = queryXml;
    }

    /// <summary>Windows Event Log QueryList XML.</summary>
    public string QueryXml { get; }

    /// <summary>Kind of source referenced by the QueryList.</summary>
    public EventLogQuerySourceKind SourceKind { get; set; }

    /// <summary>
    /// Resolves and validates the distinct channel or offline-file source kinds
    /// declared by the QueryList paths.
    /// </summary>
    public IReadOnlyList<EventLogQuerySourceKind> ResolveSourceKinds() {
        return EventLogStructuredQueryParser.ResolveSourceKinds(
            QueryXml,
            SourceKind);
    }

    /// <summary>
    /// Resolves the distinct channel names and normalized offline-file paths
    /// referenced by Query, Select, and Suppress Path attributes.
    /// </summary>
    public IReadOnlyList<EventLogStructuredQuerySource> ResolveSources() {
        return EventLogStructuredQueryParser.ResolveSources(
            QueryXml,
            SourceKind);
    }

    /// <summary>
    /// Creates an equivalent query that suppresses records at or below the
    /// source-specific lower bounds returned by <paramref name="resolver"/>.
    /// </summary>
    /// <param name="resolver">
    /// Resolves the exclusive minimum record ID for each channel or offline
    /// file. A null or non-positive value leaves that source unchanged.
    /// </param>
    public EventLogStructuredQuery WithMinimumRecordIdExclusive(
        Func<EventLogStructuredQuerySource, long?> resolver) {

        if (resolver == null) {
            throw new ArgumentNullException(nameof(resolver));
        }
        return CopyWithQueryXml(
            EventLogStructuredQueryParser
                .AddMinimumRecordIdSuppressions(
                    QueryXml,
                    SourceKind,
                    resolver));
    }

    /// <summary>
    /// Returns the number of independent native query handles required by the
    /// QueryList. Channel paths share one handle, while each distinct offline
    /// file requires its own handle.
    /// </summary>
    public int GetIndependentSourceCount() {
        return EventLogStructuredQueryParser.CountIndependentSources(
            QueryXml,
            SourceKind);
    }

    /// <summary>Remote computer name. A null or empty value targets the local computer.</summary>
    public string? MachineName { get; set; }

    /// <summary>Optional credentials used for a remote Windows Event Log session.</summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Authentication package used for a remote Windows Event Log session.</summary>
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Whether records are returned from oldest to newest.</summary>
    public bool Oldest { get; set; }

    /// <summary>Amount of event data materialized for each record.</summary>
    public EventReadMode ReadMode { get; set; } = EventReadMode.Message;

    /// <summary>Culture requested for provider messages and display names.</summary>
    public CultureInfo? MessageCulture { get; set; }

    /// <summary>Fallback culture used when provider resources do not contain MessageCulture.</summary>
    public CultureInfo? FallbackMessageCulture { get; set; }

    /// <summary>Maximum number of records returned. Zero streams every match.</summary>
    public long MaxEvents { get; set; }

    internal string? BatchSourceIdentity { get; set; }

    internal DateTime? ManagedStartTimeUtc { get; set; }

    internal DateTime? ManagedEndTimeUtc { get; set; }

    /// <summary>Materializes a native bookmark for every returned event.</summary>
    public bool IncludeBookmark { get; set; }

    /// <summary>Maximum time for remote RPC probing, worker admission, and session establishment.</summary>
    public int RemoteConnectionTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>Maximum time without remote read progress. Zero keeps the read unbounded.</summary>
    public int RemoteReadTimeoutMilliseconds { get; set; }

    /// <summary>Maximum detached event snapshots buffered between the remote native worker and caller.</summary>
    public int BufferCapacity { get; set; } = 64;

    /// <summary>RPC endpoint mapper port probed before starting a remote native query.</summary>
    public int RpcEndpointPort { get; set; } = 135;

    /// <summary>Optional native bookmark XML used as the seek origin.</summary>
    public string? BookmarkXml { get; set; }

    /// <summary>Record offset relative to <see cref="BookmarkXml"/>.</summary>
    public long BookmarkOffset { get; set; } = 1;

    /// <summary>Requires the bookmark to identify an event present in the result set.</summary>
    public bool StrictBookmark { get; set; } = true;

    /// <summary>Continues when one path in the QueryList cannot be evaluated.</summary>
    public bool TolerateQueryErrors { get; set; }

    /// <summary>
    /// Receives each path-specific failure when <see cref="TolerateQueryErrors"/> is enabled.
    /// When omitted, any path failure terminates the query so partial results cannot be mistaken
    /// for a complete result set.
    /// </summary>
    public Action<EventLogQueryFailure>? FailureHandler { get; set; }

    private EventLogStructuredQuery CopyWithQueryXml(
        string queryXml) {

        return new EventLogStructuredQuery(queryXml) {
            SourceKind = SourceKind,
            MachineName = MachineName,
            Credential = Credential,
            Authentication = Authentication,
            Oldest = Oldest,
            ReadMode = ReadMode,
            MessageCulture = MessageCulture,
            FallbackMessageCulture = FallbackMessageCulture,
            MaxEvents = MaxEvents,
            BatchSourceIdentity = BatchSourceIdentity,
            ManagedStartTimeUtc = ManagedStartTimeUtc,
            ManagedEndTimeUtc = ManagedEndTimeUtc,
            IncludeBookmark = IncludeBookmark,
            RemoteConnectionTimeoutMilliseconds =
                RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds =
                RemoteReadTimeoutMilliseconds,
            BufferCapacity = BufferCapacity,
            RpcEndpointPort = RpcEndpointPort,
            BookmarkXml = BookmarkXml,
            BookmarkOffset = BookmarkOffset,
            StrictBookmark = StrictBookmark,
            TolerateQueryErrors = TolerateQueryErrors,
            FailureHandler = FailureHandler
        };
    }

    private static string BuildQueryXml(
        IEnumerable<string> sources,
        string xpath,
        bool filePaths) {

        if (sources == null) {
            throw new ArgumentNullException(nameof(sources));
        }
        if (string.IsNullOrWhiteSpace(xpath)) {
            throw new ArgumentException(
                "XPath cannot be null or empty.",
                nameof(xpath));
        }
        string[] normalized = sources
            .Select(source => source?.Trim() ?? string.Empty)
            .Where(static source => source.Length > 0)
            .Select(source => filePaths
                ? EventLogStructuredQueryParser
                    .CreateFileSourceIdentity(source)
                : source)
            .Distinct(filePaths ? FileSystemPathIdentity.Comparer : StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length == 0) {
            throw new ArgumentException(
                "At least one source is required.",
                nameof(sources));
        }

        var builder = new StringBuilder();
        using (XmlWriter writer = XmlWriter.Create(
                   builder,
                   new XmlWriterSettings {
                       OmitXmlDeclaration = true,
                       Indent = false
                   })) {
            writer.WriteStartElement("QueryList");
            for (int index = 0; index < normalized.Length; index++) {
                writer.WriteStartElement("Query");
                writer.WriteAttributeString(
                    "Id",
                    index.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("Path", normalized[index]);
                writer.WriteStartElement("Select");
                writer.WriteAttributeString("Path", normalized[index]);
                writer.WriteString(xpath);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        return builder.ToString();
    }
}
