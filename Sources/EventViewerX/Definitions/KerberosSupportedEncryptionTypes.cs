namespace EventViewerX;

/// <summary>
/// Flags stored in <c>msDS-SupportedEncryptionTypes</c> and related Kerberos
/// event fields.
/// </summary>
[Flags]
public enum KerberosSupportedEncryptionTypes : uint
{
    /// <summary>No encryption type is explicitly defined.</summary>
    NotDefined = 0,
    /// <summary>DES-CBC-CRC is supported.</summary>
    DesCbcCrc = 0x00000001,
    /// <summary>DES-CBC-MD5 is supported.</summary>
    DesCbcMd5 = 0x00000002,
    /// <summary>RC4-HMAC is supported.</summary>
    Rc4Hmac = 0x00000004,
    /// <summary>AES128-CTS-HMAC-SHA1-96 is supported.</summary>
    Aes128CtsHmacSha1 = 0x00000008,
    /// <summary>AES256-CTS-HMAC-SHA1-96 is supported.</summary>
    Aes256CtsHmacSha1 = 0x00000010,
    /// <summary>AES session keys are enforced when a legacy ticket cipher is used.</summary>
    Aes256CtsHmacSha1SessionKey = 0x00000020,
    /// <summary>AES128-CTS-HMAC-SHA256-128 is supported.</summary>
    Aes128CtsHmacSha256 = 0x00000040,
    /// <summary>AES256-CTS-HMAC-SHA384-192 is supported.</summary>
    Aes256CtsHmacSha384 = 0x00000080,
    /// <summary>Kerberos armoring (FAST) is supported.</summary>
    Fast = 0x00010000,
    /// <summary>Compound identity is supported.</summary>
    CompoundIdentity = 0x00020000,
    /// <summary>Claims are supported.</summary>
    Claims = 0x00040000,
    /// <summary>Resource SID compression is disabled.</summary>
    ResourceSidCompressionDisabled = 0x00080000
}
