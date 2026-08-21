using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;

namespace EventViewerX.Native;

internal readonly struct NativeEventStructured {
    internal NativeEventStructured(
        NativeEventMetadata metadata,
        string xml,
        IReadOnlyList<EventPropertyValue> properties,
        EventBookmark? bookmark) {

        Metadata = metadata;
        Xml = xml;
        Properties = properties;
        Bookmark = bookmark;
    }

    internal NativeEventMetadata Metadata { get; }
    internal string Xml { get; }
    internal IReadOnlyList<EventPropertyValue> Properties { get; }
    internal EventBookmark? Bookmark { get; }
}
