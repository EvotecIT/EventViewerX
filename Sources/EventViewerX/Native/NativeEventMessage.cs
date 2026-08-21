using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;

namespace EventViewerX.Native;

internal readonly struct NativeEventMessage {
    internal NativeEventMessage(
        NativeEventMetadata metadata,
        string message,
        string levelDisplayName,
        string taskDisplayName,
        string opcodeDisplayName,
        IReadOnlyList<string> keywordDisplayNames,
        EventBookmark? bookmark,
        string cultureName,
        EventMessageRenderStatus renderStatus,
        int renderErrorCode) {

        Metadata = metadata;
        Message = message;
        LevelDisplayName = levelDisplayName;
        TaskDisplayName = taskDisplayName;
        OpcodeDisplayName = opcodeDisplayName;
        KeywordDisplayNames = keywordDisplayNames;
        Bookmark = bookmark;
        CultureName = cultureName;
        RenderStatus = renderStatus;
        RenderErrorCode = renderErrorCode;
    }

    internal NativeEventMetadata Metadata { get; }
    internal string Message { get; }
    internal string LevelDisplayName { get; }
    internal string TaskDisplayName { get; }
    internal string OpcodeDisplayName { get; }
    internal IReadOnlyList<string> KeywordDisplayNames { get; }
    internal EventBookmark? Bookmark { get; }
    internal string CultureName { get; }
    internal EventMessageRenderStatus RenderStatus { get; }
    internal int RenderErrorCode { get; }
}
