namespace EventViewerX.Providers;

/// <summary>Trust requirement applied before a provider package is installed.</summary>
public enum EventProviderPackageTrustMode {
    /// <summary>Allow unsigned packages; valid signatures are still verified.</summary>
    AllowUnsigned,
    /// <summary>Require a valid package signature.</summary>
    RequireSignature,
    /// <summary>
    /// Require a valid signature from a configured thumbprint allowlist, or
    /// when no allowlist is configured, from a Windows-trusted code-signing
    /// certificate.
    /// </summary>
    RequireTrustedSignature
}

/// <summary>Outcome of a provider package installation.</summary>
public enum EventProviderPackageInstallStatus {
    /// <summary>A new provider was installed.</summary>
    Installed,
    /// <summary>An existing provider was upgraded.</summary>
    Upgraded,
    /// <summary>The exact installed package was already active.</summary>
    Unchanged,
    /// <summary>
    /// Existing managed state or registration was repaired from verified
    /// package bytes.
    /// </summary>
    Repaired
}

/// <summary>Options controlling provider package installation.</summary>
public sealed class EventProviderPackageInstallOptions {
    /// <summary>
    /// Optional dedicated installation root. The default is the machine-wide
    /// EventViewerX provider directory under ProgramData. A custom root is
    /// claimed as a protected EventViewerX security boundary and must not
    /// contain unrelated files or directories.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Trust policy evaluated before any machine state changes.</summary>
    public EventProviderPackageTrustMode TrustMode { get; set; } =
        EventProviderPackageTrustMode.AllowUnsigned;

    /// <summary>
    /// Optional signer thumbprint allowlist for trusted-signature policy.
    /// When non-empty, the signer must match one of these exact identities.
    /// This supports private enterprise signers without trusting every
    /// certificate issued by the same authority.
    /// </summary>
    public IReadOnlyList<string> TrustedSignerThumbprints { get; set; } =
        Array.Empty<string>();

    /// <summary>Whether a lower package version may replace the active one.</summary>
    public bool AllowDowngrade { get; set; }

    /// <summary>
    /// Whether different bytes may replace an active package with the same
    /// version. Disabled by default to keep released versions immutable.
    /// </summary>
    public bool AllowSameVersionReplacement { get; set; }

    /// <summary>Maximum time allowed for each Windows registration command.</summary>
    public TimeSpan ToolTimeout { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>Result of an installed provider package operation.</summary>
public sealed class EventProviderPackageInstallResult {
    /// <summary>Installation outcome.</summary>
    public EventProviderPackageInstallStatus Status { get; internal set; }
    /// <summary>Provider name.</summary>
    public string ProviderName { get; internal set; } = string.Empty;
    /// <summary>Provider GUID.</summary>
    public Guid ProviderId { get; internal set; }
    /// <summary>Activated package version.</summary>
    public string PackageVersion { get; internal set; } = string.Empty;
    /// <summary>Version replaced by this operation, if any.</summary>
    public string PreviousVersion { get; internal set; } = string.Empty;
    /// <summary>Machine-wide provider installation directory.</summary>
    public string InstallPath { get; internal set; } = string.Empty;
    /// <summary>SHA-256 of the activated package.</summary>
    public string PackageSha256 { get; internal set; } = string.Empty;
    /// <summary>Whether package identity and contents were signed.</summary>
    public bool IsSigned { get; internal set; }
    /// <summary>Verified signer thumbprint, or an empty string.</summary>
    public string SignerThumbprint { get; internal set; } = string.Empty;
    /// <summary>Registered channel names.</summary>
    public IReadOnlyList<string> Channels { get; internal set; } =
        Array.Empty<string>();
}

/// <summary>One EventViewerX-managed provider installation.</summary>
public sealed class InstalledEventProviderPackage {
    /// <summary>Provider name.</summary>
    public string ProviderName { get; internal set; } = string.Empty;
    /// <summary>Provider GUID.</summary>
    public Guid ProviderId { get; internal set; }
    /// <summary>Active package version.</summary>
    public string PackageVersion { get; internal set; } = string.Empty;
    /// <summary>Active version directory.</summary>
    public string InstallPath { get; internal set; } = string.Empty;
    /// <summary>Archived source package path.</summary>
    public string PackagePath { get; internal set; } = string.Empty;
    /// <summary>SHA-256 of the archived package.</summary>
    public string PackageSha256 { get; internal set; } = string.Empty;
    /// <summary>UTC activation time.</summary>
    public DateTimeOffset InstalledAtUtc { get; internal set; }
    /// <summary>Whether the package was signed.</summary>
    public bool IsSigned { get; internal set; }
    /// <summary>Verified signer thumbprint, or an empty string.</summary>
    public string SignerThumbprint { get; internal set; } = string.Empty;
    /// <summary>Registered channels.</summary>
    public IReadOnlyList<string> Channels { get; internal set; } =
        Array.Empty<string>();
    /// <summary>
    /// Whether this is the package selected by EventViewerX installation
    /// state, independently of whether Windows registration is healthy.
    /// </summary>
    public bool IsActive { get; internal set; }
    /// <summary>
    /// Whether this package version is the provider currently registered with
    /// Windows. False identifies a retained schema/package.
    /// </summary>
    public bool IsRegistered { get; internal set; }
}

/// <summary>Result of removing an EventViewerX-managed provider.</summary>
public sealed class EventProviderPackageUninstallResult {
    /// <summary>Provider name.</summary>
    public string ProviderName { get; internal set; } = string.Empty;
    /// <summary>Provider GUID.</summary>
    public Guid ProviderId { get; internal set; }
    /// <summary>Previously active package version.</summary>
    public string PackageVersion { get; internal set; } = string.Empty;
    /// <summary>Whether installed package files were removed.</summary>
    public bool FilesRemoved { get; internal set; }
    /// <summary>
    /// Whether Windows retained an open resource handle and file deletion was
    /// safely scheduled for the next reboot.
    /// </summary>
    public bool FileRemovalPendingReboot { get; internal set; }
}
