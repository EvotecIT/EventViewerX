namespace EventViewerX;

/// <summary>Portable value types supported by declarative event fields.</summary>
public enum EventFieldValueKind {
    /// <summary>Text value.</summary>
    String,
    /// <summary>32-bit signed integer.</summary>
    Int32,
    /// <summary>64-bit signed integer.</summary>
    Int64,
    /// <summary>Boolean value.</summary>
    Boolean,
    /// <summary>UTC-aware date and time value.</summary>
    DateTime,
    /// <summary>Globally unique identifier.</summary>
    Guid,
    /// <summary>IPv4 or IPv6 address.</summary>
    IpAddress
}
