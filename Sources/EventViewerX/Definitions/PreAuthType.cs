namespace EventViewerX;

/// <summary>
/// Kerberos pre-authentication types observed in security events.
/// </summary>
public enum PreAuthType : int
{
    /// <summary>No pre-authentication performed.</summary>
    None = 0,
    /// <summary>Password-based timestamp (PA-ENC-TIMESTAMP).</summary>
    EncTimestamp = 2,
    /// <summary>PKINIT using X.509 certificates (PA-PK-AS-REQ).</summary>
    PublicKeyInitial = 15,
    /// <summary>PKINIT using X.509 certificates (Win2008+ variant, PA-PK-AS-REP).</summary>
    PublicKeyResponse = 17,
    /// <summary>FAST / wrapped pre-auth data (PA-FX-FAST).</summary>
    Fast = 136,
    /// <summary>Encrypted Challenge (used by Kerberos armoring).</summary>
    EncryptedChallenge = 138
}
