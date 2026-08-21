using System.Security.Cryptography.X509Certificates;

namespace PSEventViewer;

internal static class PowerShellCertificateResolver {
    internal static X509Certificate2? Resolve(
        X509Certificate2? certificate,
        string thumbprint) {

        if (certificate != null &&
            !string.IsNullOrWhiteSpace(thumbprint)) {
            throw new ArgumentException(
                "Specify SigningCertificate or CertificateThumbprint, not both.");
        }
        if (certificate != null) {
            return certificate;
        }
        if (string.IsNullOrWhiteSpace(thumbprint)) {
            return null;
        }
        string normalized = new string(
            thumbprint.Where(static character =>
                !char.IsWhiteSpace(character) &&
                character != ':').ToArray());
        foreach (StoreLocation location in new[] {
                     StoreLocation.CurrentUser,
                     StoreLocation.LocalMachine
                 }) {
            using var store = new X509Store(
                StoreName.My,
                location);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2? match = store.Certificates
                .Cast<X509Certificate2>()
                .FirstOrDefault(item =>
                    string.Equals(
                        item.Thumbprint,
                        normalized,
                        StringComparison.OrdinalIgnoreCase));
            if (match != null) {
                return match;
            }
        }
        throw new InvalidOperationException(
            $"Certificate '{thumbprint}' was not found in CurrentUser\\My or LocalMachine\\My.");
    }
}
