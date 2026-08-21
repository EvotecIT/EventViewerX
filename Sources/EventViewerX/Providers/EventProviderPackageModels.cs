namespace EventViewerX.Providers;

using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

/// <summary>Options controlling one provider package build.</summary>
public sealed class EventProviderPackageBuildOptions {
    /// <summary>
    /// Optional earlier package or definition JSON used as a compatibility
    /// baseline.
    /// </summary>
    public string BaselinePath { get; set; } = string.Empty;

    /// <summary>Whether an existing output package may be replaced.</summary>
    public bool Overwrite { get; set; }

    /// <summary>
    /// Optional RSA certificate used to sign package identity and every
    /// declared file hash. The certificate must contain its private key.
    /// </summary>
    public X509Certificate2? SigningCertificate { get; set; }
}

/// <summary>Successful provider package build result.</summary>
public sealed class EventProviderPackageBuildResult {
    /// <summary>Complete package path.</summary>
    public string OutputPath { get; internal set; } = string.Empty;
    /// <summary>Provider name embedded in the package.</summary>
    public string ProviderName { get; internal set; } = string.Empty;
    /// <summary>Provider GUID embedded in the package.</summary>
    public Guid ProviderId { get; internal set; }
    /// <summary>Provider package version.</summary>
    public string PackageVersion { get; internal set; } = string.Empty;
    /// <summary>SHA-256 hash of the completed package.</summary>
    public string PackageSha256 { get; internal set; } = string.Empty;
    /// <summary>SHA-256 hash of the compiled resource DLL.</summary>
    public string ResourceSha256 { get; internal set; } = string.Empty;
    /// <summary>Managed compiler used to create the package.</summary>
    public string Compiler { get; internal set; } = string.Empty;
    /// <summary>Non-blocking validation warnings.</summary>
    public IReadOnlyList<EventProviderValidationIssue> Warnings {
        get;
        internal set;
    } = Array.Empty<EventProviderValidationIssue>();

    /// <summary>Whether the package contains a verified signature.</summary>
    public bool IsSigned { get; internal set; }

    /// <summary>Signing certificate thumbprint, or an empty string.</summary>
    public string SignerThumbprint { get; internal set; } = string.Empty;
}

/// <summary>Metadata and file hashes stored inside a provider package.</summary>
public sealed class EventProviderPackageManifest {
    /// <summary>Package container format version.</summary>
    public int FormatVersion { get; set; } = 2;
    /// <summary>Provider name.</summary>
    public string ProviderName { get; set; } = string.Empty;
    /// <summary>Provider GUID.</summary>
    public Guid ProviderId { get; set; }
    /// <summary>Provider package version.</summary>
    public string PackageVersion { get; set; } = string.Empty;
    /// <summary>
    /// Windows SDK version recorded by legacy format-1 packages.
    /// </summary>
    [Obsolete("Format-2 packages use Compiler and CompilerVersion.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WindowsSdkVersion { get; set; }
    /// <summary>
    /// MSVC version recorded by legacy format-1 packages.
    /// </summary>
    [Obsolete("Format-2 packages use Compiler and CompilerVersion.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MsvcVersion { get; set; }
    /// <summary>Compiler implementation used to produce native resources.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Compiler { get; set; }
    /// <summary>Compiler assembly version.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompilerVersion { get; set; }
    /// <summary>Expected root files and lowercase SHA-256 hashes.</summary>
    public SortedDictionary<string, string> Files { get; set; } =
        new(StringComparer.Ordinal);

    /// <summary>Detached package-signature algorithm.</summary>
    public string SignatureAlgorithm { get; set; } = string.Empty;

    /// <summary>Base64 DER signing certificate.</summary>
    public string SigningCertificate { get; set; } = string.Empty;

    /// <summary>
    /// Base64 detached signature over package identity and declared hashes.
    /// </summary>
    public string Signature { get; set; } = string.Empty;
}

/// <summary>Verified contents of an opened provider package.</summary>
public sealed class EventProviderPackage : IDisposable {
    internal EventProviderPackage(
        string path,
        string packageSha256,
        EventProviderPackageManifest manifest,
        EventProviderDefinition definition,
        X509Certificate2? signerCertificate) {

        Path = path;
        PackageSha256 = packageSha256;
        Manifest = manifest;
        Definition = definition;
        SignerCertificate = signerCertificate;
    }

    /// <summary>Complete source package path.</summary>
    public string Path { get; }
    /// <summary>
    /// SHA-256 of the exact package bytes used for verification.
    /// </summary>
    public string PackageSha256 { get; }
    /// <summary>Verified package metadata and hashes.</summary>
    public EventProviderPackageManifest Manifest { get; }
    /// <summary>Validated provider definition.</summary>
    public EventProviderDefinition Definition { get; }

    /// <summary>
    /// Verified signing certificate, or <see langword="null"/> for an unsigned
    /// package. Trust is evaluated separately by installation policy.
    /// </summary>
    public X509Certificate2? SignerCertificate { get; }

    /// <summary>Whether the package contains a valid signature.</summary>
    public bool IsSigned => SignerCertificate != null;

    /// <summary>Releases the verified signing certificate, when present.</summary>
    public void Dispose() {
        SignerCertificate?.Dispose();
    }
}
