using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EventViewerX.Providers;

internal static class EventProviderPackageTrust {
    internal static void EnsureAllowed(
        EventProviderPackage package,
        EventProviderPackageInstallOptions options) {

        if (!Enum.IsDefined(
                typeof(EventProviderPackageTrustMode),
                options.TrustMode)) {
            throw new ArgumentOutOfRangeException(
                nameof(options.TrustMode),
                options.TrustMode,
                "Unsupported provider package trust mode.");
        }
        if (!package.IsSigned) {
            if (options.TrustMode !=
                EventProviderPackageTrustMode.AllowUnsigned) {
                throw new InvalidDataException(
                    "The provider package is unsigned, but installation policy requires a signature.");
            }
            return;
        }
        if (options.TrustMode !=
            EventProviderPackageTrustMode.RequireTrustedSignature) {
            return;
        }

        X509Certificate2 certificate = package.SignerCertificate!;
        string thumbprint = Normalize(certificate.Thumbprint);
        string[] trustedThumbprints = (
                options.TrustedSignerThumbprints ??
                Array.Empty<string>())
            .Where(static candidate =>
                !string.IsNullOrWhiteSpace(candidate))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (trustedThumbprints.Length > 0) {
            if (!trustedThumbprints.Contains(
                    thumbprint,
                    StringComparer.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    "The provider package signature is valid, but its signer does not match the configured trusted signer thumbprint allowlist.");
            }
            return;
        }

        const string CodeSigningEnhancedKeyUsage =
            "1.3.6.1.5.5.7.3.3";
        X509EnhancedKeyUsageExtension? enhancedKeyUsage =
            certificate.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .SingleOrDefault();
        if (enhancedKeyUsage == null ||
            !enhancedKeyUsage.EnhancedKeyUsages
                .Cast<Oid>()
                .Any(usage => string.Equals(
                    usage.Value,
                    CodeSigningEnhancedKeyUsage,
                    StringComparison.Ordinal))) {
            throw new InvalidDataException(
                "The provider package signature is valid, but its signer does not explicitly allow code signing.");
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode =
            X509RevocationMode.Online;
        chain.ChainPolicy.RevocationFlag =
            X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags =
            X509VerificationFlags.NoFlag;
        if (!chain.Build(certificate)) {
            string status = string.Join(
                "; ",
                chain.ChainStatus.Select(static item =>
                    item.StatusInformation.Trim()));
            throw new InvalidDataException(
                "The provider package signature is valid, but its signer is not trusted by Windows. " +
                status);
        }
    }

    private static string Normalize(string value) {
        return new string(
            (value ?? string.Empty)
            .Where(static character =>
                !char.IsWhiteSpace(character) &&
                character != ':')
            .ToArray());
    }
}
