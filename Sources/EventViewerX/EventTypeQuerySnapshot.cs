using System.Globalization;

namespace EventViewerX;

/// <summary>
/// Freezes an event-type query before deferred asynchronous execution.
/// </summary>
internal static class EventTypeQuerySnapshot {
    internal static EventTypeQuery Copy(
        EventTypeQuery source) {

        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        return new EventTypeQuery(
            source.Types.ToArray()) {
            Paths = source.Paths?.ToArray(),
            SavedEventReader = source.SavedEventReader,
            SavedEventDiagnosticHandler = source.SavedEventDiagnosticHandler,
            MachineNames =
                source.MachineNames?.ToArray(),
            CollectorLogName = string.IsNullOrWhiteSpace(source.CollectorLogName)
                ? null
                : source.CollectorLogName!.Trim(),
            SourceLogName = source.SourceLogName,
            SourceEventIds =
                source.SourceEventIds?.ToArray(),
            SourceProviderNames =
                source.SourceProviderNames?.ToArray(),
            SourceRecordIds =
                source.SourceRecordIds?.ToArray(),
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            TimePeriod = source.TimePeriod,
            MaxEvents = source.MaxEvents,
            MaxCandidates = source.MaxCandidates,
            MaxConcurrency = source.MaxConcurrency,
            Oldest = source.Oldest,
            ReadMode = source.ReadMode,
            IncludeBookmark = source.IncludeBookmark,
            Credential = EventLogCredentialIdentity.Copy(
                source.Credential),
            Authentication = source.Authentication,
            RemoteConnectionTimeoutMilliseconds =
                source.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds =
                source.RemoteReadTimeoutMilliseconds,
            BufferCapacity = source.BufferCapacity,
            MessageCulture = CopyCulture(
                source.MessageCulture),
            FallbackMessageCulture = CopyCulture(
                source.FallbackMessageCulture),
            Enrichment = CopyEnrichment(
                source.Enrichment),
            Predicate = source.Predicate?.Clone(),
            ResultPredicate = source.ResultPredicate,
            MinimumRecordIdExclusiveResolver =
                source.MinimumRecordIdExclusiveResolver,
            BookmarkXmlResolver = source.BookmarkXmlResolver,
            BookmarkOffset = source.BookmarkOffset,
            StrictBookmark = source.StrictBookmark,
            CandidateObserver = source.CandidateObserver,
            ContinueOnRemoteFailure =
                source.ContinueOnRemoteFailure
        };
    }

    private static CultureInfo? CopyCulture(
        CultureInfo? culture) {

        return culture == null
            ? null
            : CultureInfo.GetCultureInfo(
                culture.Name);
    }

    private static EventEnrichmentOptions? CopyEnrichment(
        EventEnrichmentOptions? enrichment) {

        return enrichment == null
            ? null
            : new EventEnrichmentOptions {
                ResolveDns = enrichment.ResolveDns,
                DnsTimeoutMilliseconds =
                    enrichment.DnsTimeoutMilliseconds,
                DnsMaxConcurrency =
                    enrichment.DnsMaxConcurrency,
                RetryDnsOnTransient =
                    enrichment.RetryDnsOnTransient
            };
    }
}
