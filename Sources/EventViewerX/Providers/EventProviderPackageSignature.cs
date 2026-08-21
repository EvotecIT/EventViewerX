using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace EventViewerX.Providers;

internal static class EventProviderPackageSignature {
    internal const string RsaSha256 = "RS256";

    internal static void Sign(
        EventProviderPackageManifest manifest,
        X509Certificate2 certificate) {

        if (!certificate.HasPrivateKey) {
            throw new ArgumentException(
                "The provider package signing certificate has no private key.",
                nameof(certificate));
        }
        using RSA? rsa = certificate.GetRSAPrivateKey();
        if (rsa == null) {
            throw new NotSupportedException(
                "Provider packages currently require an RSA signing certificate.");
        }

        manifest.SignatureAlgorithm = RsaSha256;
        manifest.SigningCertificate = Convert.ToBase64String(
            certificate.Export(X509ContentType.Cert));
        manifest.Signature = string.Empty;
        manifest.Signature = Convert.ToBase64String(
            rsa.SignData(
                CanonicalBytes(manifest),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1));
    }

    internal static X509Certificate2? Verify(
        EventProviderPackageManifest manifest) {

        bool hasAnySignatureValue =
            !string.IsNullOrWhiteSpace(manifest.SignatureAlgorithm) ||
            !string.IsNullOrWhiteSpace(manifest.SigningCertificate) ||
            !string.IsNullOrWhiteSpace(manifest.Signature);
        if (!hasAnySignatureValue) {
            return null;
        }
        if (!string.Equals(
                manifest.SignatureAlgorithm,
                RsaSha256,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.SigningCertificate) ||
            string.IsNullOrWhiteSpace(manifest.Signature)) {
            throw new InvalidDataException(
                "Provider package signature metadata is incomplete or unsupported.");
        }

        X509Certificate2 certificate;
        byte[] signature;
        try {
            byte[] certificateBytes =
                Convert.FromBase64String(manifest.SigningCertificate);
#if NET10_0_OR_GREATER
            certificate = X509CertificateLoader.LoadCertificate(
                certificateBytes);
#else
            certificate = new X509Certificate2(certificateBytes);
#endif
            signature = Convert.FromBase64String(manifest.Signature);
        } catch (Exception exception)
            when (exception is FormatException ||
                  exception is CryptographicException) {
            throw new InvalidDataException(
                "Provider package signature metadata is invalid.",
                exception);
        }

        using RSA? rsa = certificate.GetRSAPublicKey();
        if (rsa == null ||
            !rsa.VerifyData(
                CanonicalBytes(manifest),
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1)) {
            certificate.Dispose();
            throw new InvalidDataException(
                "Provider package signature verification failed.");
        }
        return certificate;
    }

    private static byte[] CanonicalBytes(
        EventProviderPackageManifest manifest) {

        string signature = manifest.Signature;
        try {
            manifest.Signature = string.Empty;
            return JsonSerializer.SerializeToUtf8Bytes(
                manifest,
                EventProviderDefinitionJson.SerializerOptions);
        } finally {
            manifest.Signature = signature;
        }
    }
}
