namespace EventViewerX;

/// <summary>Signature state reported while validating a detection pack.</summary>
public enum EventDetectionPackSignatureStatus {
    /// <summary>The pack does not declare a signature.</summary>
    Unsigned,
    /// <summary>A signature exists but no public key was supplied.</summary>
    Unverified,
    /// <summary>The supplied public key verified the signature.</summary>
    Valid,
    /// <summary>The supplied public key did not verify the signature.</summary>
    Invalid
}

/// <summary>Integrity, signature, and content diagnostics for a detection pack.</summary>
public sealed class EventDetectionPackValidationResult {
    internal EventDetectionPackValidationResult(
        bool contentHashValid,
        EventDetectionPackSignatureStatus signatureStatus,
        IReadOnlyList<string> diagnostics) {

        ContentHashValid = contentHashValid;
        SignatureStatus = signatureStatus;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Whether the manifest content matches its SHA-256 hash.</summary>
    public bool ContentHashValid { get; }
    /// <summary>Signature verification state.</summary>
    public EventDetectionPackSignatureStatus SignatureStatus { get; }
    /// <summary>Validation errors or warnings.</summary>
    public IReadOnlyList<string> Diagnostics { get; }
    /// <summary>Whether the pack passed all requested validation gates.</summary>
    public bool IsValid => ContentHashValid &&
                           SignatureStatus != EventDetectionPackSignatureStatus.Invalid &&
                           Diagnostics.Count == 0;
}
