using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EventViewerX.Providers;

/// <summary>
/// Installs, upgrades, inventories, and removes EventViewerX provider packages
/// without requiring build tools on the target machine.
/// </summary>
public static partial class EventProviderPackageManager {
    /// <summary>
    /// Verifies and transactionally activates one provider package. Failed
    /// upgrades restore the previously active provider.
    /// </summary>
    public static EventProviderPackageInstallResult Install(
        string packagePath,
        EventProviderPackageInstallOptions? options = null) {

        EnsureWindowsAndAdministrator();
        options ??= new EventProviderPackageInstallOptions();
        using EventProviderPackage preflightPackage =
            EventProviderPackageReader.Open(packagePath);
        EventProviderPackageTrust.EnsureAllowed(
            preflightPackage,
            options);
        using EventProviderLifecycleLock providerNameLock =
            EventProviderLifecycleLock.AcquireProviderName(
                preflightPackage.Definition.Name,
                options.ToolTimeout);
        using EventProviderLifecycleLock lifecycleLock =
            EventProviderLifecycleLock.Acquire(
                preflightPackage.Definition.Id,
                options.ToolTimeout);

        string preflightHash =
            preflightPackage.PackageSha256;
        using EventProviderPackage package =
            EventProviderPackageReader.Open(
                preflightPackage.Path);
        string packageHash =
            package.PackageSha256;
        EnsureMatchesPreflight(
            preflightPackage,
            preflightHash,
            package);
        EventProviderPackageTrust.EnsureAllowed(
            package,
            options);
        string root = ResolveRoot(options.RootPath);
        string providerRoot = Path.Combine(
            root,
            package.Definition.Id.ToString("N"));
        using (EventProviderLifecycleLock rootLock =
               EventProviderLifecycleLock.AcquireProviderRoot(
                   root,
                   options.ToolTimeout)) {
            EventProviderManagedDirectorySecurity
                .EnsureManagedRoot(
                    root,
                    options.ToolTimeout);
        }
        if (Directory.Exists(providerRoot)) {
            EventProviderManifestRegistrar.EnsureReadable(
                providerRoot,
                options.ToolTimeout);
        }
        EventProviderInstallationState? active =
            EventProviderInstallationStore.Load(providerRoot);
        if (active == null &&
            EventProviderManifestRegistrar.IsRegistered(
                package.Definition.Name)) {
            throw new InvalidOperationException(
                $"Provider '{package.Definition.Name}' is already registered outside EventViewerX package management. " +
                "Uninstall or migrate that provider before installing this package.");
        }

        EventProviderPackage? activePackage = null;
        try {
            return InstallLocked(
                package,
                packageHash,
                options,
                providerRoot,
                active,
                ref activePackage);
        } finally {
            activePackage?.Dispose();
        }
    }

    internal static void EnsureMatchesPreflight(
        EventProviderPackage preflightPackage,
        string preflightHash,
        EventProviderPackage package) {

        if (!string.Equals(
                preflightHash,
                package.PackageSha256,
                StringComparison.OrdinalIgnoreCase) ||
            preflightPackage.Definition.Id !=
            package.Definition.Id) {
            throw new InvalidDataException(
                "The provider package changed while installation was starting.");
        }
    }

    private static EventProviderPackageInstallResult InstallLocked(
        EventProviderPackage package,
        string packageHash,
        EventProviderPackageInstallOptions options,
        string providerRoot,
        EventProviderInstallationState? active,
        ref EventProviderPackage? activePackage) {

        bool registrationPresent = active != null &&
                                   EventProviderManifestRegistrar
                                       .IsRegistered(
                                           active.ProviderName);
        bool exactPackage = active != null &&
                            string.Equals(
                                active.PackageSha256,
                                packageHash,
                                StringComparison.OrdinalIgnoreCase);
        string oldDirectory = string.Empty;
        EventProviderDefinition? oldDefinition = null;
        bool oldFilesValid = false;
        if (active != null) {
            ValidateActiveIdentity(active, package.Definition);
            oldDirectory = ActiveDirectory(
                providerRoot,
                active);
            string oldArchive = Path.Combine(
                oldDirectory,
                EventProviderInstallationStore
                    .ArchivedPackageFileName);
            try {
                activePackage =
                    EventProviderPackageReader.Open(oldArchive);
                ValidateActiveIdentity(
                    active,
                    activePackage.Definition);
                if (!string.Equals(
                        active.PackageSha256,
                        EventProviderHash.FileSha256(oldArchive),
                        StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidDataException(
                        "The archived active provider package does not match installation state.");
                }
                oldDefinition = activePackage.Definition;
            } catch (Exception exception)
                when (exception is IOException ||
                      exception is InvalidDataException ||
                      exception is UnauthorizedAccessException) {
                activePackage?.Dispose();
                activePackage = null;
                if (!exactPackage) {
                    throw new InvalidDataException(
                        "The active provider package cannot be verified, so compatibility and rollback cannot be guaranteed. Reinstall the exact active package to repair it before upgrading.",
                        exception);
                }
                oldDefinition = package.Definition;
            }
            if (activePackage != null) {
                try {
                    EventProviderPackageReader
                        .EnsureExtractedFilesMatch(
                            oldArchive,
                            oldDirectory);
                    oldFilesValid = true;
                } catch (Exception exception)
                    when (exception is IOException ||
                          exception is InvalidDataException ||
                          exception is UnauthorizedAccessException) {
                }
            }
        }

        bool registrationValid = false;
        if (registrationPresent &&
            oldDefinition != null) {
            try {
                EventProviderManifestRegistrar.Verify(
                    oldDefinition);
                registrationValid = true;
            } catch (InvalidOperationException) {
            }
        }
        if (active != null &&
            exactPackage &&
            registrationValid &&
            oldFilesValid) {
            return CreateResult(
                EventProviderPackageInstallStatus.Unchanged,
                package,
                active,
                providerRoot,
                packageHash,
                active.ActiveVersion);
        }
        if (active != null && !exactPackage) {
            ValidateUpgrade(
                oldDefinition!,
                package.Definition,
                options);
        }

        string activationDirectoryName =
            CreateActivationDirectoryName(
                package.Definition.PackageVersion,
                packageHash);
        string activationDirectory = Path.Combine(
            providerRoot,
            activationDirectoryName);
        PrepareActivationDirectory(
            package.Path,
            packageHash,
            activationDirectory,
            providerRoot,
            options,
            enforceTrust: true);

        string candidateManifest = Path.Combine(
            activationDirectory,
            EventProviderPackageLayout.ManifestFileName);
        string candidateResource = Path.Combine(
            activationDirectory,
            EventProviderPackageLayout.ResourceFileName);
        if (active != null && !oldFilesValid) {
            if (activePackage != null) {
                string recoveredDirectoryName =
                    CreateActivationDirectoryName(
                        activePackage.Definition.PackageVersion,
                        active.PackageSha256);
                string recoveredDirectory = Path.Combine(
                    providerRoot,
                    recoveredDirectoryName);
                PrepareActivationDirectory(
                    activePackage.Path,
                    active.PackageSha256,
                    recoveredDirectory,
                    providerRoot,
                    options,
                    enforceTrust: false);
                oldDirectory = recoveredDirectory;
                oldFilesValid = true;
            } else if (exactPackage) {
                oldDirectory = activationDirectory;
                oldFilesValid = true;
            }
        }
        string oldManifest = active == null
            ? string.Empty
            : Path.Combine(
                oldDirectory,
                EventProviderPackageLayout.ManifestFileName);
        string oldResource = active == null
            ? string.Empty
            : Path.Combine(
                oldDirectory,
                EventProviderPackageLayout.ResourceFileName);

        bool candidateAttempted = false;
        try {
            if (registrationPresent) {
                EventProviderManifestRegistrar.Uninstall(
                    oldFilesValid
                        ? oldManifest
                        : candidateManifest,
                    options.ToolTimeout);
            }
            candidateAttempted = true;
            EventProviderManifestRegistrar.Install(
                candidateManifest,
                candidateResource,
                options.ToolTimeout);
            EventProviderManifestRegistrar.Verify(
                package.Definition);

            var newState = new EventProviderInstallationState {
                ProviderName = package.Definition.Name,
                ProviderId = package.Definition.Id,
                ActiveVersion =
                    package.Definition.PackageVersion,
                ActiveDirectoryName =
                    activationDirectoryName,
                PackageSha256 = packageHash,
                InstalledAtUtc = DateTimeOffset.UtcNow,
                IsSigned = package.IsSigned,
                SignerThumbprint =
                    package.SignerCertificate?.Thumbprint ??
                    string.Empty
            };
            EventProviderInstallationStore.Save(
                providerRoot,
                newState);
            return CreateResult(
                active == null
                    ? EventProviderPackageInstallStatus.Installed
                    : exactPackage
                        ? EventProviderPackageInstallStatus.Repaired
                        : EventProviderPackageInstallStatus.Upgraded,
                package,
                newState,
                providerRoot,
                packageHash,
                active?.ActiveVersion ?? string.Empty);
        } catch (Exception installationError) {
            Exception? rollbackError = null;
            try {
                if (candidateAttempted) {
                    if (EventProviderManifestRegistrar.IsRegistered(
                            package.Definition.Name)) {
                        EventProviderManifestRegistrar.Uninstall(
                            candidateManifest,
                            options.ToolTimeout);
                    }
                }
                if (registrationPresent &&
                    oldDefinition != null) {
                    EventProviderManifestRegistrar.Install(
                        oldManifest,
                        oldResource,
                        options.ToolTimeout);
                    EventProviderManifestRegistrar.Verify(
                        oldDefinition);
                    if (active != null &&
                        !string.Equals(
                            Path.GetFileName(oldDirectory),
                            active.ActiveDirectoryName,
                            StringComparison.Ordinal)) {
                        active.ActiveDirectoryName =
                            Path.GetFileName(oldDirectory);
                        EventProviderInstallationStore.Save(
                            providerRoot,
                            active);
                    }
                }
            } catch (Exception exception) {
                rollbackError = exception;
            }
            if (Directory.Exists(activationDirectory) &&
                !string.Equals(
                    activationDirectory,
                    oldDirectory,
                    StringComparison.OrdinalIgnoreCase)) {
                try {
                    EventProviderFileRemoval.DeleteOrSchedule(
                        activationDirectory);
                } catch (Exception cleanupError) {
                    rollbackError = rollbackError == null
                        ? cleanupError
                        : new AggregateException(
                            rollbackError,
                            cleanupError);
                }
            }
            if (rollbackError != null) {
                throw new AggregateException(
                    "Provider package installation failed and rollback could not restore the previous provider.",
                    installationError,
                    rollbackError);
            }
            throw new InvalidOperationException(
                active == null || !registrationPresent
                    ? "Provider package installation failed; the candidate provider was removed."
                    : "Provider package upgrade failed; the previous provider was restored.",
                installationError);
        }
    }

    /// <summary>Returns EventViewerX-managed provider installations.</summary>
    public static IReadOnlyList<InstalledEventProviderPackage> GetInstalled(
        string rootPath = "") {

        string root = ResolveRoot(rootPath);
        if (!Directory.Exists(root)) {
            return Array.Empty<InstalledEventProviderPackage>();
        }
        var results = new List<InstalledEventProviderPackage>();
        foreach (string providerRoot in
                 EnumerateDirectoriesSafely(root)) {
            if (Path.GetFileName(providerRoot).StartsWith(
                    ".",
                    StringComparison.Ordinal)) {
                continue;
            }
            if (!TryLoadInstallationState(
                    providerRoot,
                    out EventProviderInstallationState? state)) {
                continue;
            }
            foreach (string versionDirectory in
                     EnumerateDirectoriesSafely(providerRoot)
                         .Where(static path =>
                             !Path.GetFileName(path).StartsWith(
                                 ".",
                                 StringComparison.Ordinal))) {
                string archivePath = Path.Combine(
                    versionDirectory,
                    EventProviderInstallationStore
                        .ArchivedPackageFileName);
                if (!File.Exists(archivePath)) {
                    continue;
                }
                bool active = state != null &&
                              string.Equals(
                                  Path.GetFileName(
                                      versionDirectory),
                                  ActiveDirectoryName(state),
                                  StringComparison.Ordinal);
                try {
                    using EventProviderPackage package =
                        EventProviderPackageReader.Open(
                            archivePath);
                    EventProviderDefinition definition =
                        package.Definition;
                    string packageHash =
                        EventProviderHash.FileSha256(
                            archivePath);
                    if (active &&
                        !ActiveArchiveMatchesState(
                            definition,
                            packageHash,
                            state!)) {
                        continue;
                    }
                    bool registered = active &&
                                      EventProviderManifestRegistrar
                                          .IsRegistered(
                                              definition.Name);
                    results.Add(new InstalledEventProviderPackage {
                        ProviderName = definition.Name,
                        ProviderId = definition.Id,
                        PackageVersion =
                            definition.PackageVersion,
                        InstallPath = versionDirectory,
                        PackagePath = archivePath,
                        PackageSha256 = packageHash,
                        InstalledAtUtc = active
                            ? state!.InstalledAtUtc
                            : File.GetCreationTimeUtc(
                                archivePath),
                        IsSigned = package.IsSigned,
                        SignerThumbprint =
                            package.SignerCertificate?.Thumbprint ??
                            string.Empty,
                        Channels = definition.Channels
                            .Select(static channel => channel.Name)
                            .ToArray(),
                        IsActive = active,
                        IsRegistered = registered
                    });
                } catch (Exception exception)
                    when (IsUnreadableRetainedPackage(
                              exception)) {
                }
            }
        }
        return results
            .OrderBy(static item => item.ProviderName,
                StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(static item =>
                item.IsActive)
            .ThenByDescending(static item =>
                item.IsRegistered)
            .ThenByDescending(static item =>
                EventProviderPackageVersion.Parse(
                    item.PackageVersion))
            .ToArray();
    }

    internal static IReadOnlyList<string>
        EnumerateDirectoriesSafely(
            string path) {

        try {
            return Directory.GetDirectories(path);
        } catch (IOException) {
            return Array.Empty<string>();
        } catch (UnauthorizedAccessException) {
            return Array.Empty<string>();
        }
    }

    private static bool ActiveArchiveMatchesState(
        EventProviderDefinition definition,
        string packageHash,
        EventProviderInstallationState state) {

        return string.Equals(
                   definition.Name,
                   state.ProviderName,
                   StringComparison.Ordinal) &&
               definition.Id == state.ProviderId &&
               string.Equals(
                   definition.PackageVersion,
                   state.ActiveVersion,
                   StringComparison.Ordinal) &&
               string.Equals(
                   packageHash,
                   state.PackageSha256,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryLoadInstallationState(
        string providerRoot,
        out EventProviderInstallationState? state) {

        try {
            state = EventProviderInstallationStore.Load(
                providerRoot);
            return true;
        } catch (Exception exception)
            when (IsUnreadableInstallationState(exception)) {
            state = null;
            return false;
        }
    }

    internal static bool IsUnreadableInstallationState(
        Exception exception) {

        return exception is System.Text.Json.JsonException ||
               exception is InvalidDataException ||
               exception is IOException ||
               exception is UnauthorizedAccessException;
    }

    internal static bool IsUnreadableRetainedPackage(
        Exception exception) {

        return exception is IOException ||
               exception is InvalidDataException ||
               exception is UnauthorizedAccessException ||
               exception is System.Security.Cryptography
                   .CryptographicException ||
               exception is System.Text.Json.JsonException ||
               exception is EventProviderValidationException;
    }

    /// <summary>
    /// Loads the validated active definition for one EventViewerX-managed
    /// provider.
    /// </summary>
    public static EventProviderDefinition GetDefinition(
        string providerName,
        string rootPath = "") {

        if (string.IsNullOrWhiteSpace(providerName)) {
            throw new ArgumentException(
                "Provider name cannot be empty.",
                nameof(providerName));
        }
        InstalledEventProviderPackage installed =
            GetInstalled(rootPath).SingleOrDefault(item =>
                string.Equals(
                    item.ProviderName,
                    providerName,
                    StringComparison.OrdinalIgnoreCase) &&
                item.IsActive) ??
            throw new InvalidOperationException(
                $"Provider '{providerName}' is not managed by EventViewerX.");
        using EventProviderPackage package =
            EventProviderPackageReader.Open(
                installed.PackagePath);
        return package.Definition;
    }

    /// <summary>
    /// Unregisters an EventViewerX-managed provider. Package files are retained
    /// by default so the schema remains available for old EVTX files.
    /// </summary>
    public static EventProviderPackageUninstallResult Uninstall(
        string providerName,
        bool removeFiles = false,
        string rootPath = "",
        TimeSpan? toolTimeout = null) {

        EnsureWindowsAndAdministrator();
        if (string.IsNullOrWhiteSpace(providerName)) {
            throw new ArgumentException(
                "Provider name cannot be empty.",
                nameof(providerName));
        }
        TimeSpan timeout =
            toolTimeout ?? TimeSpan.FromMinutes(1);
        providerName = providerName.Trim();
        using EventProviderLifecycleLock providerNameLock =
            EventProviderLifecycleLock.AcquireProviderName(
                providerName,
                timeout);
        InstalledEventProviderPackage initial =
            GetInstalled(rootPath).SingleOrDefault(item =>
                string.Equals(
                    item.ProviderName,
                    providerName,
                    StringComparison.OrdinalIgnoreCase) &&
                item.IsActive) ??
            throw new InvalidOperationException(
                $"Provider '{providerName}' is not managed by EventViewerX.");
        using EventProviderLifecycleLock lifecycleLock =
            EventProviderLifecycleLock.Acquire(
                initial.ProviderId,
                timeout);
        InstalledEventProviderPackage installed =
            GetInstalled(rootPath).SingleOrDefault(item =>
                string.Equals(
                    item.ProviderName,
                    providerName,
                    StringComparison.OrdinalIgnoreCase) &&
                item.ProviderId == initial.ProviderId &&
                item.IsActive) ??
            throw new InvalidOperationException(
                $"Provider '{providerName}' changed while waiting for its lifecycle lock.");
        string providerRoot = Directory.GetParent(
            installed.InstallPath)!.FullName;
        string manifestDirectory = installed.InstallPath;
        try {
            EventProviderPackageReader.EnsureExtractedFilesMatch(
                installed.PackagePath,
                manifestDirectory);
        } catch (Exception exception)
            when (IsRecoverableActivePayloadFailure(exception)) {
            string recoveredDirectoryName =
                CreateActivationDirectoryName(
                    installed.PackageVersion,
                    installed.PackageSha256);
            manifestDirectory = Path.Combine(
                providerRoot,
                recoveredDirectoryName);
            PrepareActivationDirectory(
                installed.PackagePath,
                installed.PackageSha256,
                manifestDirectory,
                providerRoot,
                new EventProviderPackageInstallOptions {
                    RootPath = rootPath,
                    ToolTimeout = timeout
                },
                enforceTrust: false);
        }
        if (EventProviderManifestRegistrar.IsRegistered(
                installed.ProviderName)) {
            EventProviderManifestRegistrar.Uninstall(
                Path.Combine(
                    manifestDirectory,
                    EventProviderPackageLayout.ManifestFileName),
                timeout);
        }

        string statePath = Path.Combine(
            providerRoot,
            EventProviderInstallationStore.StateFileName);
        if (File.Exists(statePath)) {
            File.Delete(statePath);
        }
        bool filesRemoved = false;
        bool pendingReboot = false;
        if (removeFiles) {
            filesRemoved =
                EventProviderFileRemoval.DeleteOrSchedule(
                    providerRoot);
            pendingReboot = !filesRemoved;
        }
        return new EventProviderPackageUninstallResult {
            ProviderName = installed.ProviderName,
            ProviderId = installed.ProviderId,
            PackageVersion = installed.PackageVersion,
            FilesRemoved = filesRemoved,
            FileRemovalPendingReboot = pendingReboot
        };
    }

    internal static bool IsRecoverableActivePayloadFailure(
        Exception exception) {

        return exception is InvalidDataException ||
               exception is IOException ||
               exception is UnauthorizedAccessException;
    }

    /// <summary>Returns the conventional machine-wide provider package root.</summary>
    public static string GetDefaultRootPath() {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "EventViewerX",
            "Providers");
    }

}
