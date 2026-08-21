using System.Globalization;

namespace EventViewerX;

/// <summary>
/// Freezes mutable query options before deferred or concurrent execution.
/// </summary>
internal static class EventLogQuerySnapshot {
    internal static EventLogChannelQuery Copy(
        EventLogChannelQuery source,
        long outerLimit = 0) {

        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        return new EventLogChannelQuery(source.LogName) {
            MachineName = source.MachineName,
            Credential =
                EventLogCredentialIdentity.Copy(
                    source.Credential),
            Authentication = source.Authentication,
            XPath = source.XPath,
            Oldest = source.Oldest,
            ReadMode = source.ReadMode,
            MessageCulture = CopyCulture(source.MessageCulture),
            FallbackMessageCulture =
                CopyCulture(source.FallbackMessageCulture),
            MaxEvents = ApplyLimit(source.MaxEvents, outerLimit),
            BatchSourceIdentity =
                source.BatchSourceIdentity,
            ManagedStartTimeUtc = source.ManagedStartTimeUtc,
            ManagedEndTimeUtc = source.ManagedEndTimeUtc,
            ManagedPredicate = source.ManagedPredicate,
            ManagedMaxEventsScanned = source.ManagedMaxEventsScanned,
            ManagedScanLimitReached = source.ManagedScanLimitReached,
            IncludeBookmark = source.IncludeBookmark,
            RemoteConnectionTimeoutMilliseconds =
                source.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds =
                source.RemoteReadTimeoutMilliseconds,
            BufferCapacity = source.BufferCapacity,
            RpcEndpointPort = source.RpcEndpointPort,
            BookmarkXml = source.BookmarkXml,
            BookmarkOffset = source.BookmarkOffset,
            StrictBookmark = source.StrictBookmark
        };
    }

    internal static EventLogFileQuery Copy(
        EventLogFileQuery source,
        long outerLimit = 0) {

        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        return new EventLogFileQuery(source.Path) {
            XPath = source.XPath,
            Oldest = source.Oldest,
            ReadMode = source.ReadMode,
            MessageCulture = CopyCulture(source.MessageCulture),
            FallbackMessageCulture =
                CopyCulture(source.FallbackMessageCulture),
            MaxEvents = ApplyLimit(source.MaxEvents, outerLimit),
            BatchSourceIdentity =
                source.BatchSourceIdentity,
            IncludeBookmark = source.IncludeBookmark,
            BookmarkXml = source.BookmarkXml,
            BookmarkOffset = source.BookmarkOffset,
            StrictBookmark = source.StrictBookmark
        };
    }

    internal static EventLogStructuredQuery Copy(
        EventLogStructuredQuery source,
        long outerLimit = 0) {

        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        return new EventLogStructuredQuery(source.QueryXml) {
            SourceKind = source.SourceKind,
            MachineName = source.MachineName,
            Credential =
                EventLogCredentialIdentity.Copy(
                    source.Credential),
            Authentication = source.Authentication,
            Oldest = source.Oldest,
            ReadMode = source.ReadMode,
            MessageCulture = CopyCulture(source.MessageCulture),
            FallbackMessageCulture =
                CopyCulture(source.FallbackMessageCulture),
            MaxEvents = ApplyLimit(source.MaxEvents, outerLimit),
            BatchSourceIdentity =
                source.BatchSourceIdentity,
            ManagedStartTimeUtc = source.ManagedStartTimeUtc,
            ManagedEndTimeUtc = source.ManagedEndTimeUtc,
            IncludeBookmark = source.IncludeBookmark,
            RemoteConnectionTimeoutMilliseconds =
                source.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds =
                source.RemoteReadTimeoutMilliseconds,
            BufferCapacity = source.BufferCapacity,
            RpcEndpointPort = source.RpcEndpointPort,
            BookmarkXml = source.BookmarkXml,
            BookmarkOffset = source.BookmarkOffset,
            StrictBookmark = source.StrictBookmark,
            TolerateQueryErrors = source.TolerateQueryErrors,
            FailureHandler = source.FailureHandler
        };
    }

    private static CultureInfo? CopyCulture(
        CultureInfo? culture) {

        return culture == null
            ? null
            : CultureInfo.GetCultureInfo(culture.Name);
    }

    private static long ApplyLimit(
        long sourceLimit,
        long outerLimit) {

        if (outerLimit <= 0) {
            return sourceLimit;
        }
        if (sourceLimit <= 0) {
            return outerLimit;
        }
        return Math.Min(sourceLimit, outerLimit);
    }
}
