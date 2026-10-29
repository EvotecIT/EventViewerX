using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;
#if NET472
using System.Runtime.Serialization;
#endif

namespace EventViewerX.Native;

/// <summary>
/// Provides a target-independent bookmark XML representation without repeating native rendering.
/// </summary>
internal static class EventBookmarkXml {
    private static readonly ConditionalWeakTable<EventBookmark, BookmarkText> Cache = new();

    internal static string Get(EventBookmark bookmark) {
        if (bookmark == null) {
            throw new ArgumentNullException(nameof(bookmark));
        }

        return Cache.GetValue(bookmark, static value => new BookmarkText(Read(value))).Value;
    }

    internal static void Register(EventBookmark bookmark, string bookmarkXml) {
        if (bookmark == null) {
            throw new ArgumentNullException(nameof(bookmark));
        }
        if (string.IsNullOrWhiteSpace(bookmarkXml)) {
            throw new ArgumentException("Bookmark XML cannot be empty.", nameof(bookmarkXml));
        }

        Cache.Remove(bookmark);
        Cache.Add(bookmark, new BookmarkText(bookmarkXml));
    }

    private static string Read(EventBookmark bookmark) {
#if NET472
        var information = new SerializationInfo(
            typeof(EventBookmark),
            new FormatterConverter());
        ((ISerializable)bookmark).GetObjectData(
            information,
            new StreamingContext(StreamingContextStates.All));
        return information.GetString("BookmarkText") ?? string.Empty;
#else
        return bookmark.BookmarkXml;
#endif
    }

    private sealed class BookmarkText {
        internal BookmarkText(string value) {
            Value = value;
        }

        internal string Value { get; }
    }
}
