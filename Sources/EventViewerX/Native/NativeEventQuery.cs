using System;

namespace EventViewerX.Native;

internal readonly struct NativeEventQuery {
    internal NativeEventQuery(
        IntPtr session,
        string? path,
        string xpath,
        WindowsEventNativeMethods.QueryFlags flags,
        string displayName,
        string? publisherMetadataPath = null,
        int messageLocale = 0,
        int fallbackMessageLocale = 0,
        int nextTimeoutMilliseconds = 0,
        bool includeBookmark = false,
        string? bookmarkXml = null,
        long bookmarkOffset = 1,
        bool strictBookmark = true,
        string? machineName = null,
        Action<EventLogQueryFailure>? failureHandler = null) {

        Session = session;
        Path = path;
        XPath = xpath;
        Flags = flags;
        DisplayName = displayName;
        PublisherMetadataPath = publisherMetadataPath;
        MessageLocale = messageLocale;
        FallbackMessageLocale = fallbackMessageLocale;
        NextTimeoutMilliseconds = nextTimeoutMilliseconds;
        IncludeBookmark = includeBookmark;
        BookmarkXml = bookmarkXml;
        BookmarkOffset = bookmarkOffset;
        StrictBookmark = strictBookmark;
        MachineName = machineName;
        FailureHandler = failureHandler;
    }

    internal IntPtr Session { get; }
    internal string? Path { get; }
    internal string XPath { get; }
    internal WindowsEventNativeMethods.QueryFlags Flags { get; }
    internal string DisplayName { get; }
    internal string? PublisherMetadataPath { get; }
    internal int MessageLocale { get; }
    internal int FallbackMessageLocale { get; }
    internal int NextTimeoutMilliseconds { get; }
    internal bool IncludeBookmark { get; }
    internal string? BookmarkXml { get; }
    internal long BookmarkOffset { get; }
    internal bool StrictBookmark { get; }
    internal string? MachineName { get; }
    internal Action<EventLogQueryFailure>? FailureHandler { get; }

    internal NativeEventQuery WithFileSource(
        string nativePath,
        string? structuredQuery = null) {

        return new NativeEventQuery(
            Session,
            Path == null ? null : nativePath,
            structuredQuery ?? XPath,
            Flags,
            DisplayName,
            nativePath,
            MessageLocale,
            FallbackMessageLocale,
            NextTimeoutMilliseconds,
            IncludeBookmark,
            BookmarkXml,
            BookmarkOffset,
            StrictBookmark,
            MachineName,
            FailureHandler);
    }
}
