namespace EventViewerX.Providers;

/// <summary>Windows Event Log channel kind declared by a provider.</summary>
public enum EventProviderChannelType {
    /// <summary>Administrator-facing events that require action.</summary>
    Admin,
    /// <summary>Operational events used to diagnose and monitor a component.</summary>
    Operational,
    /// <summary>High-volume analytic events, disabled by default.</summary>
    Analytic,
    /// <summary>High-volume debugging events, disabled by default.</summary>
    Debug
}

/// <summary>Security and backing-log isolation used by a provider channel.</summary>
public enum EventProviderChannelIsolation {
    /// <summary>Use the Application channel isolation and defaults.</summary>
    Application,
    /// <summary>Use the System channel isolation and defaults.</summary>
    System,
    /// <summary>Use a custom backing log and channel access descriptor.</summary>
    Custom
}

/// <summary>Supported Windows manifest input types for event payload fields.</summary>
public enum EventProviderFieldType {
    /// <summary>
    /// Infer the field type from a typed payload property. This value is only
    /// valid on <see cref="EventProviderPayloadFieldAttribute"/>.
    /// </summary>
    Auto = -1,
    /// <summary>Null-terminated UTF-16 string.</summary>
    UnicodeString,
    /// <summary>Null-terminated string encoded with the Windows ANSI code page.</summary>
    AnsiString,
    /// <summary>Signed 8-bit integer.</summary>
    Int8,
    /// <summary>Unsigned 8-bit integer.</summary>
    UInt8,
    /// <summary>Signed 16-bit integer.</summary>
    Int16,
    /// <summary>Unsigned 16-bit integer.</summary>
    UInt16,
    /// <summary>Signed 32-bit integer.</summary>
    Int32,
    /// <summary>Unsigned 32-bit integer.</summary>
    UInt32,
    /// <summary>Signed 64-bit integer.</summary>
    Int64,
    /// <summary>Unsigned 64-bit integer.</summary>
    UInt64,
    /// <summary>Single-precision floating-point number.</summary>
    Float,
    /// <summary>Double-precision floating-point number.</summary>
    Double,
    /// <summary>32-bit Windows Boolean value.</summary>
    Boolean,
    /// <summary>Length-delimited binary data.</summary>
    Binary,
    /// <summary>16-byte GUID.</summary>
    Guid,
    /// <summary>Native pointer-sized unsigned value.</summary>
    Pointer,
    /// <summary>64-bit Windows file time.</summary>
    FileTime,
    /// <summary>16-byte Windows SYSTEMTIME value.</summary>
    SystemTime,
    /// <summary>Binary Windows security identifier.</summary>
    Sid,
    /// <summary>Signed 32-bit integer rendered as hexadecimal.</summary>
    HexInt32,
    /// <summary>Signed 64-bit integer rendered as hexadecimal.</summary>
    HexInt64
}

/// <summary>Common rendering hints for Windows manifest payload fields.</summary>
public enum EventProviderFieldOutputType {
    /// <summary>Use the default output type for the selected input type.</summary>
    Default,
    /// <summary>Render as an XML Schema string.</summary>
    String,
    /// <summary>Render as an XML Schema date and time.</summary>
    DateTime,
    /// <summary>Render a date and time independently of the viewer culture.</summary>
    CultureInsensitiveDateTime,
    /// <summary>Render as XML text.</summary>
    Xml,
    /// <summary>Render as JSON text.</summary>
    Json,
    /// <summary>Render binary input as UTF-8 text.</summary>
    Utf8,
    /// <summary>Render as an HRESULT.</summary>
    HResult,
    /// <summary>Render as a Win32 error code.</summary>
    ErrorCode,
    /// <summary>Render as an NTSTATUS value.</summary>
    NtStatus,
    /// <summary>Render as a process identifier.</summary>
    Pid,
    /// <summary>Render as a thread identifier.</summary>
    Tid,
    /// <summary>Render as a network port.</summary>
    Port,
    /// <summary>Render as an IPv4 address.</summary>
    IPv4,
    /// <summary>Render as an IPv6 address.</summary>
    IPv6,
    /// <summary>Render as a socket address.</summary>
    SocketAddress,
    /// <summary>Render as a code pointer.</summary>
    CodePointer
}

/// <summary>Map representation used to render numeric payload values.</summary>
public enum EventProviderMapKind {
    /// <summary>One message is selected for one exact numeric value.</summary>
    Value,
    /// <summary>Messages are combined for the bits present in a numeric value.</summary>
    Bit
}
