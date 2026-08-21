using EventViewerX.Providers;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventProviderPackageInventory {
    [Fact]
    public void StateSaveCleanupDoesNotReplaceThePrimaryFailure() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-State-" +
            Guid.NewGuid().ToString("N"));
        string statePath = Path.Combine(
            root,
            EventProviderInstallationStore
                .StateFileName);
        Directory.CreateDirectory(statePath);
        try {
            IOException exception =
                Assert.Throws<IOException>(() =>
                    EventProviderInstallationStore.Save(
                        root,
                        new EventProviderInstallationState(),
                        static _ =>
                            throw new InvalidOperationException(
                                "Cleanup failed.")));

            Assert.DoesNotContain(
                "Cleanup failed.",
                exception.Message,
                StringComparison.Ordinal);
        } finally {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void MissingProviderDirectoryIsIgnoredDuringInventory() {
        string missing = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));

        Assert.Empty(
            EventProviderPackageManager
                .EnumerateDirectoriesSafely(
                    missing));
    }

    [Fact]
    public void CorruptInstallationStateDoesNotHideAnotherProvider() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        string healthyProviderRoot = Path.Combine(
            root,
            definition.Id.ToString("N"));
        string activeDirectory = Path.Combine(
            healthyProviderRoot,
            "active");
        string activePackage = Path.Combine(
            activeDirectory,
            EventProviderInstallationStore
                .ArchivedPackageFileName);
        string corruptProviderRoot = Path.Combine(
            root,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(activeDirectory);
        Directory.CreateDirectory(corruptProviderRoot);
        try {
            EventProviderPackageBuildResult build =
                EventProviderPackageBuilder.Build(
                    definition,
                    activePackage);
            EventProviderInstallationStore.Save(
                healthyProviderRoot,
                new EventProviderInstallationState {
                    ProviderName = definition.Name,
                    ProviderId = definition.Id,
                    ActiveVersion =
                        definition.PackageVersion,
                    ActiveDirectoryName =
                        Path.GetFileName(activeDirectory),
                    PackageSha256 =
                        build.PackageSha256,
                    InstalledAtUtc =
                        DateTimeOffset.UtcNow
                });
            File.WriteAllText(
                Path.Combine(
                    corruptProviderRoot,
                    EventProviderInstallationStore
                        .StateFileName),
                "{not valid json");

            InstalledEventProviderPackage installed =
                Assert.Single(
                    EventProviderPackageManager
                        .GetInstalled(root));

            Assert.True(installed.IsActive);
            Assert.Equal(
                definition.Id,
                installed.ProviderId);
        } finally {
            if (Directory.Exists(root)) {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void CorruptRetainedPackageDoesNotHideHealthyActivePackage() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        string providerRoot = Path.Combine(
            root,
            definition.Id.ToString("N"));
        string activeDirectory = Path.Combine(
            providerRoot,
            "active");
        string retainedDirectory = Path.Combine(
            providerRoot,
            "retained-corrupt");
        Directory.CreateDirectory(activeDirectory);
        Directory.CreateDirectory(retainedDirectory);
        string activePackage = Path.Combine(
            activeDirectory,
            EventProviderInstallationStore
                .ArchivedPackageFileName);
        try {
            EventProviderPackageBuildResult build =
                EventProviderPackageBuilder.Build(
                    definition,
                    activePackage);
            File.WriteAllText(
                Path.Combine(
                    retainedDirectory,
                    EventProviderInstallationStore
                        .ArchivedPackageFileName),
                "not a provider package");
            EventProviderInstallationStore.Save(
                providerRoot,
                new EventProviderInstallationState {
                    ProviderName = definition.Name,
                    ProviderId = definition.Id,
                    ActiveVersion =
                        definition.PackageVersion,
                    ActiveDirectoryName =
                        Path.GetFileName(activeDirectory),
                    PackageSha256 =
                        build.PackageSha256,
                    InstalledAtUtc =
                        DateTimeOffset.UtcNow
                });

            InstalledEventProviderPackage installed =
                Assert.Single(
                    EventProviderPackageManager
                        .GetInstalled(root));
            EventProviderDefinition activeDefinition =
                EventProviderPackageManager
                    .GetDefinition(
                        definition.Name,
                        root);

            Assert.True(installed.IsActive);
            Assert.Equal(
                definition.Name,
                installed.ProviderName);
            Assert.Equal(
                definition.Id,
                activeDefinition.Id);
        } finally {
            if (Directory.Exists(root)) {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void CorruptActivePackageDoesNotHideAnotherProvider() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        EventProviderDefinition healthyDefinition =
            TestEventProviderPackages.CreateDefinition();
        EventProviderDefinition corruptDefinition =
            TestEventProviderPackages.CreateDefinition();
        corruptDefinition.Name =
            "EventViewerX.Tests.CorruptActive";
        corruptDefinition.Id = Guid.NewGuid();
        corruptDefinition.Channels[0].Name =
            "EventViewerX.Tests.CorruptActive/Operational";
        try {
            CreateInstalledPackage(
                root,
                healthyDefinition,
                corrupt: false);
            CreateInstalledPackage(
                root,
                corruptDefinition,
                corrupt: true);

            InstalledEventProviderPackage installed =
                Assert.Single(
                    EventProviderPackageManager
                        .GetInstalled(root));

            Assert.Equal(
                healthyDefinition.Id,
                installed.ProviderId);
            Assert.True(installed.IsActive);
        } finally {
            if (Directory.Exists(root)) {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void ValidReplacementArchiveCannotImpersonateActiveState() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        EventProviderDefinition original =
            TestEventProviderPackages.CreateDefinition();
        EventProviderDefinition replacement =
            TestEventProviderPackages.CreateDefinition();
        replacement.Name =
            "EventViewerX.Tests.Replacement";
        replacement.Id = Guid.NewGuid();
        replacement.Channels[0].Name =
            replacement.Name +
            "/Operational";
        string providerRoot = Path.Combine(
            root,
            original.Id.ToString("N"));
        string activeDirectory = Path.Combine(
            providerRoot,
            "active");
        string activePackage = Path.Combine(
            activeDirectory,
            EventProviderInstallationStore
                .ArchivedPackageFileName);
        string replacementPackage = Path.Combine(
            root,
            "replacement.evxprovider");
        Directory.CreateDirectory(activeDirectory);
        try {
            EventProviderPackageBuildResult originalBuild =
                EventProviderPackageBuilder.Build(
                    original,
                    activePackage);
            EventProviderPackageBuilder.Build(
                replacement,
                replacementPackage);
            EventProviderInstallationStore.Save(
                providerRoot,
                new EventProviderInstallationState {
                    ProviderName = original.Name,
                    ProviderId = original.Id,
                    ActiveVersion =
                        original.PackageVersion,
                    ActiveDirectoryName = "active",
                    PackageSha256 =
                        originalBuild.PackageSha256,
                    InstalledAtUtc =
                        DateTimeOffset.UtcNow
                });
            File.Copy(
                replacementPackage,
                activePackage,
                overwrite: true);

            Assert.Empty(
                EventProviderPackageManager
                    .GetInstalled(root));
            Assert.Throws<InvalidOperationException>(() =>
                EventProviderPackageManager
                    .GetDefinition(
                        original.Name,
                        root));
            Assert.Throws<InvalidOperationException>(() =>
                EventProviderPackageManager
                    .GetDefinition(
                        replacement.Name,
                        root));
        } finally {
            if (Directory.Exists(root)) {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    private static void CreateInstalledPackage(
        string root,
        EventProviderDefinition definition,
        bool corrupt) {

        string providerRoot = Path.Combine(
            root,
            definition.Id.ToString("N"));
        string activeDirectory = Path.Combine(
            providerRoot,
            "active");
        string packagePath = Path.Combine(
            activeDirectory,
            EventProviderInstallationStore
                .ArchivedPackageFileName);
        Directory.CreateDirectory(activeDirectory);
        EventProviderPackageBuildResult build =
            EventProviderPackageBuilder.Build(
                definition,
                packagePath);
        EventProviderInstallationStore.Save(
            providerRoot,
            new EventProviderInstallationState {
                ProviderName = definition.Name,
                ProviderId = definition.Id,
                ActiveVersion = definition.PackageVersion,
                ActiveDirectoryName = "active",
                PackageSha256 = build.PackageSha256,
                InstalledAtUtc = DateTimeOffset.UtcNow
            });
        if (corrupt) {
            File.WriteAllText(
                packagePath,
                "not a provider package");
        }
    }
}
