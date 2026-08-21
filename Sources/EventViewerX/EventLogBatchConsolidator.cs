using System.Globalization;
using System.Net;
using System.Xml.Linq;

namespace EventViewerX;

/// <summary>
/// Consolidates compatible batch sources into native QueryList sessions.
/// Native consolidation deduplicates overlapping Select expressions without retaining an unbounded managed identity set.
/// </summary>
public static class EventLogBatchConsolidator {
    /// <summary>
    /// Combines compatible queries by target session and native source kind.
    /// Channels and files remain separate native handles because Windows accepts
    /// one source-kind flag per handle. Per-group options must agree; separate
    /// remote machines remain separate sessions.
    /// </summary>
    public static EventLogBatchQuery Consolidate(
        EventLogBatchQuery query) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventLogChannelQuery[] forwarded = query.ChannelQueries
            .Where(static channel =>
                string.Equals(
                    channel.LogName,
                    ForwardedEventsQuerySafety.ChannelName,
                    StringComparison.OrdinalIgnoreCase))
            .Select(static channel =>
                EventLogQuerySnapshot.Copy(channel))
            .ToArray();
        if (forwarded.Length > 0) {
            EventLogBatchQuery forwardedBatch =
                EventLogBatchQuery.ForChannels(forwarded);
            CopyControls(query, forwardedBatch);
            var remainderParts = new List<EventLogBatchQuery>();
            EventLogChannelQuery[] otherChannels = query.ChannelQueries
                .Where(static channel =>
                    !string.Equals(
                        channel.LogName,
                        ForwardedEventsQuerySafety.ChannelName,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (otherChannels.Length > 0) {
                remainderParts.Add(
                    EventLogBatchQuery.ForChannels(otherChannels));
            }
            if (query.FileQueries.Count > 0) {
                remainderParts.Add(
                    EventLogBatchQuery.ForFiles(query.FileQueries));
            }
            if (query.StructuredQueries.Count > 0) {
                remainderParts.Add(
                    EventLogBatchQuery.ForStructured(
                        query.StructuredQueries));
            }
            foreach (EventLogBatchQuery part in remainderParts) {
                CopyControls(query, part);
            }
            if (remainderParts.Count == 0) {
                return forwardedBatch;
            }
            EventLogBatchQuery remainder = Consolidate(
                EventLogBatchQuery.Combine(remainderParts));
            return EventLogBatchQuery.Combine(
                new[] { forwardedBatch, remainder });
        }
        QueryInput[] inputs = Snapshot(query);
        if (inputs.Length == 0) {
            throw new ArgumentException(
                "The batch does not contain any query sources.",
                nameof(query));
        }

        EventLogStructuredQuery[] consolidated = inputs
            .GroupBy(
                static input => new {
                    MachineName = (
                        input.Profile.MachineName ??
                        string.Empty).ToUpperInvariant(),
                    input.SourceKind,
                    SourceIdentity =
                        input.SourceIdentity.ToUpperInvariant(),
                    ConsolidationScope =
                        input.ConsolidationScope.ToUpperInvariant(),
                    input.Profile.ManagedStartTimeUtc,
                    input.Profile.ManagedEndTimeUtc,
                    input.TolerateQueryErrors,
                    input.FailureHandler
                })
            .Select(CreateStructuredQuery)
            .ToArray();
        EventLogBatchQuery result =
            EventLogBatchQuery.ForStructured(consolidated);
        result.MaxEvents = query.MaxEvents;
        result.MaxConcurrency = query.MaxConcurrency;
        result.ContinueOnError = query.ContinueOnError;
        result.FailureHandler = query.FailureHandler;
        return result;
    }

    private static void CopyControls(
        EventLogBatchQuery source,
        EventLogBatchQuery target) {

        target.MaxEvents = source.MaxEvents;
        target.MaxConcurrency = source.MaxConcurrency;
        target.ContinueOnError = source.ContinueOnError;
        target.FailureHandler = source.FailureHandler;
    }

    private static EventLogStructuredQuery CreateStructuredQuery(
        IEnumerable<QueryInput> group) {

        QueryInput[] inputs = group.ToArray();
        QueryProfile profile = inputs[0].Profile;
        foreach (QueryInput input in inputs.Skip(1)) {
            profile.ValidateCompatible(input.Profile);
        }

        var root = new XElement("QueryList");
        var queryKeys = new HashSet<string>(
            StringComparer.Ordinal);
        int id = 0;
        foreach (QueryInput input in inputs) {
            foreach (XElement sourceQuery in input.Queries) {
                var query = new XElement(sourceQuery);
                query.SetAttributeValue("Id", null);
                string queryKey = query.ToString(
                    SaveOptions.DisableFormatting);
                if (!queryKeys.Add(queryKey)) {
                    continue;
                }
                query.SetAttributeValue(
                    "Id",
                    id.ToString(CultureInfo.InvariantCulture));
                root.Add(query);
                id++;
            }
        }
        var structured =
            new EventLogStructuredQuery(
                root.ToString(SaveOptions.DisableFormatting)) {
                SourceKind = inputs[0].SourceKind,
                MachineName = profile.MachineName,
                Credential = profile.Credential,
                Authentication = profile.Authentication,
                Oldest = profile.Oldest,
                ReadMode = profile.ReadMode,
                MessageCulture = profile.MessageCulture,
                FallbackMessageCulture =
                    profile.FallbackMessageCulture,
                MaxEvents = profile.MaxEvents,
                BatchSourceIdentity =
                    profile.BatchSourceIdentity,
                ManagedStartTimeUtc = profile.ManagedStartTimeUtc,
                ManagedEndTimeUtc = profile.ManagedEndTimeUtc,
                IncludeBookmark = profile.IncludeBookmark,
                RemoteConnectionTimeoutMilliseconds =
                    profile.RemoteConnectionTimeoutMilliseconds,
                RemoteReadTimeoutMilliseconds =
                    profile.RemoteReadTimeoutMilliseconds,
                BufferCapacity = profile.BufferCapacity,
                RpcEndpointPort = profile.RpcEndpointPort,
                BookmarkXml = profile.BookmarkXml,
                BookmarkOffset = profile.BookmarkOffset,
                StrictBookmark = profile.StrictBookmark,
                TolerateQueryErrors =
                    inputs[0].TolerateQueryErrors,
                FailureHandler =
                    inputs[0].FailureHandler
            };
        return structured;
    }

    private static QueryInput[] Snapshot(
        EventLogBatchQuery query) {

        var inputs = new List<QueryInput>(
            query.ChannelQueries.Count +
            query.FileQueries.Count +
            query.StructuredQueries.Count);
        foreach (EventLogChannelQuery channel in
                 query.ChannelQueries) {
            QueryProfile profile =
                QueryProfile.From(channel);
            XElement queryElement =
                CreateQueryElement(
                    channel.LogName,
                    channel.XPath);
            inputs.Add(new QueryInput(
                profile,
                EventLogQuerySourceKind.Channel,
                sourceIdentity: string.Empty,
                new[] { queryElement },
                consolidationScope:
                    GetConsolidationScope(
                        profile,
                        new[] { queryElement })));
        }
        foreach (EventLogFileQuery file in
                 query.FileQueries) {
            string source =
                EventLogStructuredQueryParser
                    .CreateFileSourceIdentity(
                        file.Path);
            QueryProfile profile =
                QueryProfile.From(file);
            XElement queryElement =
                CreateQueryElement(
                    source,
                    file.XPath);
            inputs.Add(new QueryInput(
                profile,
                EventLogQuerySourceKind.File,
                source,
                new[] { queryElement },
                consolidationScope:
                    GetConsolidationScope(
                        profile,
                        new[] { queryElement })));
        }
        foreach (EventLogStructuredQuery structured in
                 query.StructuredQueries) {
            QueryProfile profile =
                QueryProfile.From(structured);
            XElement[] queries =
                EventLogStructuredQueryParser.ParseQueries(
                    structured.QueryXml);
            string consolidationScope =
                GetConsolidationScope(
                    profile,
                    queries);
            foreach (XElement queryElement in queries) {
                EventLogQuerySourceKind sourceKind =
                    EventLogStructuredQueryParser.ResolveSourceKind(
                        queryElement,
                        structured.SourceKind);
                inputs.Add(new QueryInput(
                    profile,
                    sourceKind,
                    sourceKind == EventLogQuerySourceKind.File
                        ? EventLogStructuredQueryParser
                            .GetFileSourceIdentity(queryElement)
                        : string.Empty,
                    new[] { new XElement(queryElement) },
                    consolidationScope,
                    structured.TolerateQueryErrors,
                    structured.FailureHandler));
            }
        }
        return inputs.ToArray();
    }

    private static string GetConsolidationScope(
        QueryProfile profile,
        IReadOnlyList<XElement> queries) {

        if (profile.MaxEvents <= 0) {
            return string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(
                profile.BatchSourceIdentity)) {
            return "logical:" +
                   profile.BatchSourceIdentity;
        }
        return string.Join(
            "\n",
            queries
                .Select(static sourceQuery => {
                    var query =
                        new XElement(sourceQuery);
                    query.SetAttributeValue("Id", null);
                    return query.ToString(
                        SaveOptions.DisableFormatting);
                })
                .OrderBy(
                    static query =>
                        query,
                    StringComparer.Ordinal));
    }

    private static XElement CreateQueryElement(
        string source,
        string xpath) {

        return new XElement(
            "Query",
            new XAttribute("Path", source),
            new XElement(
                "Select",
                new XAttribute("Path", source),
                xpath));
    }

    private sealed class QueryInput {
        internal QueryInput(
            QueryProfile profile,
            EventLogQuerySourceKind sourceKind,
            string sourceIdentity,
            IReadOnlyList<XElement> queries,
            string consolidationScope = "",
            bool tolerateQueryErrors = false,
            Action<EventLogQueryFailure>? failureHandler = null) {

            Profile = profile;
            SourceKind = sourceKind;
            SourceIdentity = sourceIdentity;
            Queries = queries;
            ConsolidationScope = consolidationScope;
            TolerateQueryErrors = tolerateQueryErrors;
            FailureHandler = failureHandler;
        }

        internal QueryProfile Profile { get; }
        internal EventLogQuerySourceKind SourceKind { get; }
        internal string SourceIdentity { get; }
        internal IReadOnlyList<XElement> Queries { get; }
        internal string ConsolidationScope { get; }
        internal bool TolerateQueryErrors { get; }
        internal Action<EventLogQueryFailure>? FailureHandler {
            get;
        }
    }

    private sealed class QueryProfile {
        private QueryProfile() {
        }

        internal string? MachineName { get; set; }
        internal NetworkCredential? Credential { get; set; }
        internal EventLogAuthentication Authentication {
            get;
            set;
        }
        internal bool Oldest { get; set; }
        internal EventReadMode ReadMode { get; set; }
        internal CultureInfo? MessageCulture { get; set; }
        internal CultureInfo? FallbackMessageCulture {
            get;
            set;
        }
        internal long MaxEvents { get; set; }
        internal string? BatchSourceIdentity { get; set; }
        internal DateTime? ManagedStartTimeUtc { get; set; }
        internal DateTime? ManagedEndTimeUtc { get; set; }
        internal bool IncludeBookmark { get; set; }
        internal int RemoteConnectionTimeoutMilliseconds {
            get;
            set;
        } = 5000;
        internal int RemoteReadTimeoutMilliseconds {
            get;
            set;
        }
        internal int BufferCapacity { get; set; } = 64;
        internal int RpcEndpointPort { get; set; } = 135;
        internal string? BookmarkXml { get; set; }
        internal long BookmarkOffset { get; set; } = 1;
        internal bool StrictBookmark { get; set; } = true;

        internal static QueryProfile From(
            EventLogChannelQuery query) {

            return new QueryProfile {
                MachineName = NormalizeMachine(
                    query.MachineName),
                Credential = EventLogCredentialIdentity.Copy(
                    query.Credential),
                Authentication = query.Authentication,
                Oldest = query.Oldest,
                ReadMode = query.ReadMode,
                MessageCulture = query.MessageCulture,
                FallbackMessageCulture =
                    query.FallbackMessageCulture,
                MaxEvents = query.MaxEvents,
                BatchSourceIdentity =
                    query.BatchSourceIdentity,
                ManagedStartTimeUtc = query.ManagedStartTimeUtc,
                ManagedEndTimeUtc = query.ManagedEndTimeUtc,
                IncludeBookmark = query.IncludeBookmark,
                RemoteConnectionTimeoutMilliseconds =
                    query.RemoteConnectionTimeoutMilliseconds,
                RemoteReadTimeoutMilliseconds =
                    query.RemoteReadTimeoutMilliseconds,
                BufferCapacity = query.BufferCapacity,
                RpcEndpointPort = query.RpcEndpointPort,
                BookmarkXml = query.BookmarkXml,
                BookmarkOffset = query.BookmarkOffset,
                StrictBookmark = query.StrictBookmark
            };
        }

        internal static QueryProfile From(
            EventLogFileQuery query) {

            return new QueryProfile {
                Oldest = query.Oldest,
                ReadMode = query.ReadMode,
                MessageCulture = query.MessageCulture,
                FallbackMessageCulture =
                    query.FallbackMessageCulture,
                MaxEvents = query.MaxEvents,
                BatchSourceIdentity =
                    query.BatchSourceIdentity,
                IncludeBookmark = query.IncludeBookmark,
                BookmarkXml = query.BookmarkXml,
                BookmarkOffset = query.BookmarkOffset,
                StrictBookmark = query.StrictBookmark
            };
        }

        internal static QueryProfile From(
            EventLogStructuredQuery query) {

            return new QueryProfile {
                MachineName = NormalizeMachine(
                    query.MachineName),
                Credential = EventLogCredentialIdentity.Copy(
                    query.Credential),
                Authentication = query.Authentication,
                Oldest = query.Oldest,
                ReadMode = query.ReadMode,
                MessageCulture = query.MessageCulture,
                FallbackMessageCulture =
                    query.FallbackMessageCulture,
                MaxEvents = query.MaxEvents,
                BatchSourceIdentity =
                    query.BatchSourceIdentity,
                ManagedStartTimeUtc = query.ManagedStartTimeUtc,
                ManagedEndTimeUtc = query.ManagedEndTimeUtc,
                IncludeBookmark = query.IncludeBookmark,
                RemoteConnectionTimeoutMilliseconds =
                    query.RemoteConnectionTimeoutMilliseconds,
                RemoteReadTimeoutMilliseconds =
                    query.RemoteReadTimeoutMilliseconds,
                BufferCapacity = query.BufferCapacity,
                RpcEndpointPort = query.RpcEndpointPort,
                BookmarkXml = query.BookmarkXml,
                BookmarkOffset = query.BookmarkOffset,
                StrictBookmark = query.StrictBookmark
            };
        }

        internal void ValidateCompatible(
            QueryProfile other) {

            if (Oldest != other.Oldest ||
                ReadMode != other.ReadMode ||
                MaxEvents != other.MaxEvents ||
                ManagedStartTimeUtc != other.ManagedStartTimeUtc ||
                ManagedEndTimeUtc != other.ManagedEndTimeUtc ||
                IncludeBookmark != other.IncludeBookmark ||
                !string.Equals(
                    MessageCulture?.Name,
                    other.MessageCulture?.Name,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    FallbackMessageCulture?.Name,
                    other.FallbackMessageCulture?.Name,
                    StringComparison.OrdinalIgnoreCase) ||
                Authentication != other.Authentication ||
                !EventLogCredentialIdentity.AreEqual(
                    Credential,
                    other.Credential) ||
                RemoteConnectionTimeoutMilliseconds !=
                other.RemoteConnectionTimeoutMilliseconds ||
                RemoteReadTimeoutMilliseconds !=
                other.RemoteReadTimeoutMilliseconds ||
                BufferCapacity != other.BufferCapacity ||
                RpcEndpointPort != other.RpcEndpointPort ||
                !string.Equals(
                    BookmarkXml,
                    other.BookmarkXml,
                    StringComparison.Ordinal) ||
                BookmarkOffset != other.BookmarkOffset ||
                StrictBookmark != other.StrictBookmark) {
                throw new ArgumentException(
                    $"Batch queries targeting '{MachineName ?? Environment.MachineName}' cannot be consolidated because their read/session options differ.");
            }
        }

        private static string? NormalizeMachine(
            string? machineName) {

            return EventLogTarget.IsLocalMachine(machineName)
                ? null
                : machineName?.Trim();
        }

    }
}
