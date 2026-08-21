namespace EventViewerX;

/// <summary>Identifies where a custom event-definition field obtains its value.</summary>
public enum EventFieldSource {
    /// <summary>Named EventData or UserData value.</summary>
    Data,
    /// <summary>Public EventObject property.</summary>
    Metadata,
    /// <summary>Parsed message-data value.</summary>
    MessageData,
    /// <summary>Complete rendered provider message.</summary>
    Message,
    /// <summary>Literal value from the definition.</summary>
    Constant
}
