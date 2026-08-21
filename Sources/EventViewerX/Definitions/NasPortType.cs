namespace EventViewerX;

/// <summary>
/// Identifies the type of network access server port from the RADIUS NAS-Port-Type attribute.
/// </summary>
public enum NasPortType {
    /// <summary>Asynchronous serial.</summary>
    Async = 0,
    /// <summary>Synchronous serial.</summary>
    Sync = 1,
    /// <summary>ISDN.</summary>
    ISDN = 2,
    /// <summary>ISDN V.120.</summary>
    ISDNV120 = 3,
    /// <summary>ISDN V.110.</summary>
    ISDNV110 = 4,
    /// <summary>Virtual (no physical port).</summary>
    Virtual = 5,
    /// <summary>PIAFS.</summary>
    PIAFS = 6,
    /// <summary>HDLC clear channel.</summary>
    HdlcClearChannel = 7,
    /// <summary>X.25.</summary>
    X25 = 8,
    /// <summary>X.75.</summary>
    X75 = 9,
    /// <summary>Group 3 Fax.</summary>
    G3Fax = 10,
    /// <summary>SDSL.</summary>
    SDSL = 11,
    /// <summary>ADSL (CAP).</summary>
    ADSLCAP = 12,
    /// <summary>ADSL (DMT).</summary>
    ADSLDMT = 13,
    /// <summary>IDSL.</summary>
    IDSL = 14,
    /// <summary>Ethernet.</summary>
    Ethernet = 15,
    /// <summary>xDSL (other).</summary>
    XDSL = 16,
    /// <summary>Cable modem.</summary>
    Cable = 17,
    /// <summary>Wireless (unspecified).</summary>
    WirelessOther = 18,
    /// <summary>Wireless 802.11.</summary>
    WirelessIEEE80211 = 19,
    /// <summary>Token Ring.</summary>
    TokenRing = 20,
    /// <summary>FDDI.</summary>
    FDDI = 21,
    /// <summary>Wireless CDMA.</summary>
    WirelessCDMA = 22,
    /// <summary>Wireless CDPD.</summary>
    WirelessCDPD = 23,
    /// <summary>Wireless iDEN.</summary>
    WirelessIDEN = 24,
    /// <summary>Wireless 1xEV.</summary>
    Wireless1XEV = 25,
    /// <summary>Inter-access point protocol.</summary>
    IAPP = 26,
    /// <summary>Fiber to the premises.</summary>
    FTTP = 27
}
