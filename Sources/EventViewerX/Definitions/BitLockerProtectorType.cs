namespace EventViewerX;

/// <summary>
/// Protector types available for BitLocker encryption.
/// </summary>
public enum BitLockerProtectorType {
    /// <summary>Trusted Platform Module.</summary>
    Tpm = 2200,
    /// <summary>External key (e.g., USB) protector.</summary>
    ExternalKey = 2201,
    /// <summary>Numerical recovery password.</summary>
    NumericalPassword = 2202,
    /// <summary>TPM with PIN.</summary>
    TpmPin = 2203,
    /// <summary>TPM with startup key.</summary>
    TpmStartupKey = 2204,
    /// <summary>TPM with both PIN and startup key.</summary>
    TpmPinAndStartupKey = 2205,
    /// <summary>User passphrase protector.</summary>
    Passphrase = 2206,
    /// <summary>Recovery key file.</summary>
    RecoveryKey = 2207
}
