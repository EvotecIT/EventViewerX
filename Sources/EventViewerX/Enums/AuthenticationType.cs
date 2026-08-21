namespace EventViewerX;

/// <summary>
/// Typical RADIUS authentication types.
/// </summary>
public enum AuthenticationType {
    /// <summary>Authentication type is not recognized.</summary>
    Unknown,

    /// <summary>PAP or clear text authentication.</summary>
    PAP,

    /// <summary>Challenge Handshake Authentication Protocol.</summary>
    CHAP,

    /// <summary>Microsoft CHAP.</summary>
    MSCHAP,

    /// <summary>Microsoft CHAP version 2.</summary>
    MSCHAPv2,

    /// <summary>Extensible Authentication Protocol.</summary>
    EAP
}
