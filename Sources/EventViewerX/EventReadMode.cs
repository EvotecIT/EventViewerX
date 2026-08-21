namespace EventViewerX;

/// <summary>
/// Controls how much provider data is materialized while reading an event.
/// </summary>
public enum EventReadMode {
    /// <summary>
    /// Captures core event metadata only. Provider message formatting, XML parsing, and native bookmark
    /// materialization are skipped.
    /// </summary>
    Metadata,

    /// <summary>
    /// Captures metadata and the provider-formatted message without reading event XML.
    /// </summary>
    Message,

    /// <summary>
    /// Captures metadata and structured XML data without formatting the provider message.
    /// </summary>
    StructuredData,

    /// <summary>
    /// Captures core metadata and raw event XML without formatting the provider
    /// message or projecting typed payload values. This is the lowest-cost
    /// mode for XML streaming and export.
    /// </summary>
    RawXml,

    /// <summary>
    /// Captures metadata, the formatted message, structured XML data, and attachments.
    /// </summary>
    Full,

    /// <summary>
    /// Captures metadata, the formatted message, and structured XML data without decoding binary attachments.
    /// This is the preferred mode for typed event projection and reporting.
    /// </summary>
    StructuredDataAndMessage
}
