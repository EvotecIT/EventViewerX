using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EventViewerX.Providers;

public static partial class EventProviderPackageManager {
    internal static void ValidateUpgrade(
        EventProviderDefinition baseline,
        EventProviderDefinition candidate,
        EventProviderPackageInstallOptions options) {

        EventProviderPackageVersion baselineVersion =
            EventProviderPackageVersion.Parse(
                baseline.PackageVersion);
        EventProviderPackageVersion candidateVersion =
            EventProviderPackageVersion.Parse(
                candidate.PackageVersion);
        int comparison = candidateVersion.CompareTo(
            baselineVersion);
        if (comparison < 0 && !options.AllowDowngrade) {
            throw new InvalidOperationException(
                $"Provider package downgrade from {baseline.PackageVersion} to {candidate.PackageVersion} is disabled.");
        }
        if (comparison < 0) {
            return;
        }
        if (comparison == 0 &&
            !options.AllowSameVersionReplacement) {
            throw new InvalidOperationException(
                $"Provider package version {candidate.PackageVersion} is already active with different bytes. Publish a new immutable version or explicitly allow same-version replacement.");
        }
        EventProviderCompatibility.EnsureCompatible(
            baseline,
            candidate);
    }

    private static void PrepareActivationDirectory(
        string packagePath,
        string packageHash,
        string activationDirectory,
        string providerRoot,
        EventProviderPackageInstallOptions options,
        bool enforceTrust) {

        Directory.CreateDirectory(providerRoot);
        EventProviderManifestRegistrar.EnsureReadable(
            providerRoot,
            options.ToolTimeout);
        string staging = Path.Combine(
            providerRoot,
            ".staging-" + Guid.NewGuid().ToString("N"));
        try {
            using EventProviderPackage extracted =
                EventProviderPackageReader.Extract(
                    packagePath,
                    staging);
            File.Copy(
                packagePath,
                Path.Combine(
                    staging,
                    EventProviderInstallationStore
                        .ArchivedPackageFileName));
            string archivedPath = Path.Combine(
                staging,
                EventProviderInstallationStore
                    .ArchivedPackageFileName);
            if (!string.Equals(
                    EventProviderHash.FileSha256(archivedPath),
                    packageHash,
                    StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    "The provider package changed while it was being staged.");
            }
            using (EventProviderPackage archived =
                   EventProviderPackageReader.Open(
                       archivedPath)) {
                if (enforceTrust) {
                    EventProviderPackageTrust.EnsureAllowed(
                        archived,
                        options);
                }
            }
            EventProviderPackageReader.EnsureExtractedFilesMatch(
                archivedPath,
                staging);
            EventProviderManifestRegistrar.EnsureReadable(
                staging,
                options.ToolTimeout);
            PromoteActivationDirectory(
                staging,
                activationDirectory);
        } finally {
            CleanupStagingDirectory(staging);
        }
    }

    internal static void PromoteActivationDirectory(
        string staging,
        string activationDirectory,
        Action<string, string>? moveDirectory = null,
        Action<string>? removeDirectory = null) {

        moveDirectory ??= Directory.Move;
        removeDirectory ??=
            path => EventProviderFileRemoval
                .DeleteOrSchedule(path);
        if (!Directory.Exists(activationDirectory)) {
            moveDirectory(
                staging,
                activationDirectory);
            return;
        }

        string backup =
            activationDirectory +
            ".replaced-" +
            Guid.NewGuid().ToString("N");
        moveDirectory(
            activationDirectory,
            backup);
        try {
            moveDirectory(
                staging,
                activationDirectory);
        } catch (Exception promotionError) {
            Exception? restoreError = null;
            try {
                if (!Directory.Exists(activationDirectory) &&
                    Directory.Exists(backup)) {
                    moveDirectory(
                        backup,
                        activationDirectory);
                }
            } catch (Exception exception) {
                restoreError = exception;
            }
            if (restoreError != null) {
                throw new AggregateException(
                    "Provider activation promotion failed and the previous activation directory could not be restored.",
                    promotionError,
                    restoreError);
            }
            throw;
        }
        try {
            removeDirectory(backup);
        } catch {
            // The replacement is authoritative. Deferred cleanup must not
            // replace a successful activation.
        }
    }

    /// <summary>
    /// Removes an invocation-owned staging directory without replacing the
    /// activation result or its primary failure when cleanup is blocked.
    /// </summary>
    internal static void CleanupStagingDirectory(
        string staging,
        Action<string, bool>? deleteDirectory = null) {

        deleteDirectory ??= Directory.Delete;
        try {
            if (Directory.Exists(staging)) {
                deleteDirectory(
                    staging,
                    true);
            }
        } catch (Exception) {
        }
    }

    private static EventProviderPackageInstallResult CreateResult(
        EventProviderPackageInstallStatus status,
        EventProviderPackage package,
        EventProviderInstallationState state,
        string providerRoot,
        string packageHash,
        string previousVersion) {

        return new EventProviderPackageInstallResult {
            Status = status,
            ProviderName = package.Definition.Name,
            ProviderId = package.Definition.Id,
            PackageVersion =
                package.Definition.PackageVersion,
            PreviousVersion = previousVersion,
            InstallPath = VersionDirectory(
                providerRoot,
                ActiveDirectoryName(state)),
            PackageSha256 = packageHash,
            IsSigned = package.IsSigned,
            SignerThumbprint =
                package.SignerCertificate?.Thumbprint ??
                string.Empty,
            Channels = package.Definition.Channels
                .Select(static channel => channel.Name)
                .ToArray()
        };
    }

    private static void ValidateActiveIdentity(
        EventProviderInstallationState active,
        EventProviderDefinition candidate) {

        if (!string.Equals(
                active.ProviderName,
                candidate.Name,
                StringComparison.Ordinal) ||
            active.ProviderId != candidate.Id) {
            throw new InvalidDataException(
                "Provider installation state does not match the candidate identity.");
        }
    }

    private static string VersionDirectory(
        string providerRoot,
        string directoryName) {

        if (string.IsNullOrWhiteSpace(directoryName) ||
            !string.Equals(
                Path.GetFileName(directoryName),
                directoryName,
                StringComparison.Ordinal) ||
            directoryName.IndexOfAny(
                Path.GetInvalidFileNameChars()) >= 0) {
            throw new InvalidDataException(
                "Provider installation state contains an invalid activation directory.");
        }
        return Path.Combine(providerRoot, directoryName);
    }

    private static string ActiveDirectory(
        string providerRoot,
        EventProviderInstallationState state) {

        return VersionDirectory(
            providerRoot,
            ActiveDirectoryName(state));
    }

    private static string ActiveDirectoryName(
        EventProviderInstallationState state) {

        return string.IsNullOrWhiteSpace(
            state.ActiveDirectoryName)
            ? state.ActiveVersion
            : state.ActiveDirectoryName;
    }

    private static string CreateActivationDirectoryName(
        string version,
        string packageHash) {

        _ = EventProviderPackageVersion.Parse(version);
        return version + "-" +
               packageHash.Substring(0, 12)
                   .ToLowerInvariant() +
               "-" +
               Guid.NewGuid().ToString("N")
                   .Substring(0, 8);
    }

    private static string ResolveRoot(string rootPath) {
        return Path.GetFullPath(
            string.IsNullOrWhiteSpace(rootPath)
                ? GetDefaultRootPath()
                : rootPath);
    }

    private static void EnsureWindowsAndAdministrator() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            throw new PlatformNotSupportedException(
                "Windows event provider packages can only be installed on Windows.");
        }
        using WindowsIdentity identity =
            WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (!principal.IsInRole(
                WindowsBuiltInRole.Administrator)) {
            throw new UnauthorizedAccessException(
                "Installing or removing a Windows event provider requires an elevated administrator process.");
        }
    }
}
