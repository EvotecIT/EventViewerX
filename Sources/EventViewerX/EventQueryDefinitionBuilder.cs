using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>Builds detached high-level event query definitions.</summary>
public sealed class EventQueryDefinitionBuilder {
    private IReadOnlyList<string>? _logNames;
    private IReadOnlyList<string>? _paths;
    private IReadOnlyList<string>? _providerNames;
    private string? _queryXml;

    /// <summary>Typed native filter.</summary>
    public EventFilter? Filter { get; set; }
    /// <summary>Raw XPath applied to every resolved channel or file.</summary>
    public string? FilterXPath { get; set; }
    /// <summary>Local or remote targets. Empty means local.</summary>
    public IEnumerable<string?>? MachineNames { get; set; }
    /// <summary>Query projection, ordering, remote-session, and batch controls.</summary>
    public EventLogQueryOptionsBuilder Options { get; } = new();
    /// <summary>Includes wildcard-matched analytic and debug channels.</summary>
    public bool IncludeAnalyticAndDebugChannels { get; set; }
    /// <summary>Allows QueryList paths unsupported on a target.</summary>
    public bool TolerateQueryErrors { get; set; }

    /// <summary>Selects channel names or wildcard patterns.</summary>
    public EventQueryDefinitionBuilder FromChannels(params string[] logNames) {
        SelectSource(logNames, null, null, null);
        return this;
    }

    /// <summary>Selects saved EVTX paths or wildcard patterns.</summary>
    public EventQueryDefinitionBuilder FromFiles(params string[] paths) {
        SelectSource(null, paths, null, null);
        return this;
    }

    /// <summary>Selects provider names or wildcard patterns.</summary>
    public EventQueryDefinitionBuilder FromProviders(params string[] providerNames) {
        SelectSource(null, null, providerNames, null);
        return this;
    }

    /// <summary>Selects a complete Windows Event Log QueryList.</summary>
    public EventQueryDefinitionBuilder FromQueryXml(string queryXml) {
        SelectSource(null, null, null, queryXml);
        return this;
    }

    /// <summary>Validates and detaches the current query request.</summary>
    public EventQueryDefinition Build() {
        int sourceCount = HasValues(_logNames) + HasValues(_paths) + HasValues(_providerNames) +
                          (string.IsNullOrWhiteSpace(_queryXml) ? 0 : 1);
        if (sourceCount != 1) {
            throw new InvalidOperationException(
                "Select exactly one query source with FromChannels, FromFiles, FromProviders, or FromQueryXml.");
        }
        string? xpath = FilterXPath?.Trim();
        if (Filter?.HasAny == true && xpath is { Length: > 0 }) {
            throw new InvalidOperationException("Filter and FilterXPath cannot be combined.");
        }
        if (_queryXml != null && (Filter?.HasAny == true || xpath is { Length: > 0 })) {
            throw new InvalidOperationException("QueryXml cannot be combined with Filter or FilterXPath.");
        }
        return new EventQueryDefinition {
            LogNames = _logNames?.ToArray(),
            Paths = _paths?.ToArray(),
            ProviderNames = _providerNames?.ToArray(),
            QueryXml = _queryXml,
            Filter = Filter?.Clone(),
            FilterXPath = xpath,
            MachineNames = MachineNames?.ToArray(),
            Options = Options.Build(),
            IncludeAnalyticAndDebugChannels = IncludeAnalyticAndDebugChannels,
            TolerateQueryErrors = TolerateQueryErrors
        };
    }

    private void SelectSource(
        IEnumerable<string>? logNames,
        IEnumerable<string>? paths,
        IEnumerable<string>? providers,
        string? queryXml) {

        _logNames = Normalize(logNames, StringComparer.OrdinalIgnoreCase);
        _paths = NormalizePaths(paths);
        _providerNames = Normalize(providers, StringComparer.OrdinalIgnoreCase);
        _queryXml = string.IsNullOrWhiteSpace(queryXml) ? null : queryXml!.Trim();
    }

    private static int HasValues(IReadOnlyList<string>? values) => values is { Count: > 0 } ? 1 : 0;

    private static string[]? Normalize(IEnumerable<string>? values, StringComparer comparer) {
        if (values == null) {
            return null;
        }
        string[] result = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(comparer)
            .ToArray();
        return result.Length == 0 ? null : result;
    }

    private static string[]? NormalizePaths(IEnumerable<string>? paths) {
        if (paths == null) {
            return null;
        }
        string[] result = FileSystemPathIdentity.NormalizeUnresolvedPaths(paths);
        return result.Length == 0 ? null : result;
    }
}

/// <summary>Builds detached common event-query controls.</summary>
public sealed class EventLogQueryOptionsBuilder {
    /// <summary>Whether records are returned oldest first.</summary>
    public bool Oldest { get; set; }
    /// <summary>Amount of event data materialized.</summary>
    public EventReadMode ReadMode { get; set; } = EventReadMode.Message;
    /// <summary>Requested provider-message culture.</summary>
    public CultureInfo? MessageCulture { get; set; }
    /// <summary>Fallback provider-resource culture.</summary>
    public CultureInfo? FallbackMessageCulture { get; set; }
    /// <summary>Maximum merged results. Zero is unlimited.</summary>
    public long MaxEvents { get; set; }
    /// <summary>Maximum managed compatibility candidates. Zero is unlimited.</summary>
    public long MaxEventsScanned { get; set; }
    /// <summary>Whether each result includes a native bookmark.</summary>
    public bool IncludeBookmark { get; set; }
    /// <summary>Native bookmark XML seek origin.</summary>
    public string? BookmarkXml { get; set; }
    /// <summary>Record offset relative to the bookmark.</summary>
    public long BookmarkOffset { get; set; } = 1;
    /// <summary>Whether a missing bookmark boundary fails.</summary>
    public bool StrictBookmark { get; set; } = true;
    /// <summary>Remote credentials. The built result receives a detached copy.</summary>
    public NetworkCredential? Credential { get; set; }
    /// <summary>Remote authentication package.</summary>
    public EventLogAuthentication Authentication { get; set; }
    /// <summary>Remote connection timeout in milliseconds.</summary>
    public int RemoteConnectionTimeoutMilliseconds { get; set; } = 5000;
    /// <summary>Remote no-progress timeout in milliseconds.</summary>
    public int RemoteReadTimeoutMilliseconds { get; set; }
    /// <summary>Per-reader detached buffer capacity.</summary>
    public int BufferCapacity { get; set; } = 64;
    /// <summary>RPC endpoint mapper port.</summary>
    public int RpcEndpointPort { get; set; } = 135;
    /// <summary>Maximum independent sources opened concurrently.</summary>
    public int MaxConcurrency { get; set; } = 8;
    /// <summary>Whether healthy sources continue after another fails.</summary>
    public bool ContinueOnError { get; set; }
    /// <summary>Receives isolated source failures.</summary>
    public Action<EventLogQueryFailure>? FailureHandler { get; set; }

    /// <summary>Validates and detaches the current controls.</summary>
    public EventLogQueryOptions Build() {
        if (MaxEvents < 0 || MaxEventsScanned < 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxEvents), "Query limits cannot be negative.");
        }
        if (BookmarkOffset < 0) {
            throw new ArgumentOutOfRangeException(nameof(BookmarkOffset));
        }
        if (RemoteConnectionTimeoutMilliseconds <= 0 || RemoteReadTimeoutMilliseconds < 0 ||
            BufferCapacity <= 0 || RpcEndpointPort is < 1 or > 65535 || MaxConcurrency <= 0) {
            throw new ArgumentOutOfRangeException(nameof(RemoteConnectionTimeoutMilliseconds),
                "Timeouts, buffer capacity, RPC port, and concurrency must be within their documented bounds.");
        }
        return new EventLogQueryOptions {
            Oldest = Oldest,
            ReadMode = ReadMode,
            MessageCulture = CopyCulture(MessageCulture),
            FallbackMessageCulture = CopyCulture(FallbackMessageCulture),
            MaxEvents = MaxEvents,
            MaxEventsScanned = MaxEventsScanned,
            IncludeBookmark = IncludeBookmark,
            BookmarkXml = BookmarkXml?.Trim(),
            BookmarkOffset = BookmarkOffset,
            StrictBookmark = StrictBookmark,
            Credential = EventLogCredentialIdentity.Copy(Credential),
            Authentication = Authentication,
            RemoteConnectionTimeoutMilliseconds = RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = RemoteReadTimeoutMilliseconds,
            BufferCapacity = BufferCapacity,
            RpcEndpointPort = RpcEndpointPort,
            MaxConcurrency = MaxConcurrency,
            ContinueOnError = ContinueOnError,
            FailureHandler = FailureHandler
        };
    }

    private static CultureInfo? CopyCulture(CultureInfo? culture) =>
        culture == null ? null : CultureInfo.GetCultureInfo(culture.Name);
}
