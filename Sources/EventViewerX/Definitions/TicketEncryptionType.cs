namespace EventViewerX;

/// <summary>
/// Identifies encryption algorithms used for Kerberos tickets.
/// </summary>
public enum TicketEncryptionType : uint
{
    /// <summary>DES CBC with CRC checksum.</summary>
    DES_CBC_CRC = 0x1,
    /// <summary>DES CBC with MD5 checksum.</summary>
    DES_CBC_MD5 = 0x3,
    /// <summary>AES-128 with HMAC-SHA1.</summary>
    AES128_CTS_HMAC_SHA1_96 = 0x11,
    /// <summary>AES-256 with HMAC-SHA1.</summary>
    AES256_CTS_HMAC_SHA1_96 = 0x12,
    /// <summary>RC4 with HMAC.</summary>
    RC4_HMAC = 0x17,
    /// <summary>Export-strength RC4 with HMAC.</summary>
    RC4_HMAC_EXP = 0x18,
    /// <summary>AES-128 with HMAC-SHA256 (Windows 10+).</summary>
    AES128_CTS_HMAC_SHA256_128 = 0x2C,
    /// <summary>AES-256 with HMAC-SHA384 (Windows 10+).</summary>
    AES256_CTS_HMAC_SHA384_192 = 0x2D,
    /// <summary>Indicates audit failure / unknown type.</summary>
    AuditFailure = 0xFFFFFFFF,

    // Friendly aliases
    /// <summary>Alias for <see cref="AES128_CTS_HMAC_SHA1_96"/>.</summary>
    AES128 = AES128_CTS_HMAC_SHA1_96,
    /// <summary>Alias for <see cref="AES256_CTS_HMAC_SHA1_96"/>.</summary>
    AES256 = AES256_CTS_HMAC_SHA1_96
}
