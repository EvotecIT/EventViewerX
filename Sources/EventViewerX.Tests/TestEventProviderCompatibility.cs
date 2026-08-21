using EventViewerX.Providers;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventProviderCompatibility {
    [Fact]
    public void RejectsTaskEventGuidChanges() {
        EventProviderDefinition baseline =
            TestEventProviderPackages.CreateDefinition();
        baseline.Tasks.Add(
            new EventProviderTaskDefinition {
                Name = "ScanTask",
                Value = 1,
                EventGuid = Guid.NewGuid()
            });
        EventProviderDefinition candidate =
            TestEventProviderPackages.CreateDefinition();
        candidate.Tasks.Add(
            new EventProviderTaskDefinition {
                Name = "ScanTask",
                Value = 1,
                EventGuid = Guid.NewGuid()
            });

        EventProviderCompatibilityResult result =
            EventProviderCompatibility.Compare(
                baseline,
                candidate);

        Assert.False(result.IsCompatible);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     "TaskEventGuidChanged");
    }

    [Fact]
    public void AllowedDowngradeDoesNotApplyForwardCompatibilityRules() {
        EventProviderDefinition active =
            TestEventProviderPackages.CreateDefinition();
        active.PackageVersion = "2.0.0";
        active.Channels.Add(
            EventProviderChannelDefinition.Operational(
                "AddedLater",
                "Evotec-EventViewerX-PackageTest/AddedLater"));
        EventProviderDefinition older =
            TestEventProviderPackages.CreateDefinition();
        older.PackageVersion = "1.0.0";

        EventProviderPackageManager.ValidateUpgrade(
            active,
            older,
            new EventProviderPackageInstallOptions {
                AllowDowngrade = true
            });

        InvalidOperationException disabled =
            Assert.Throws<InvalidOperationException>(() =>
                EventProviderPackageManager.ValidateUpgrade(
                    active,
                    older,
                    new EventProviderPackageInstallOptions()));
        Assert.Contains(
            "downgrade",
            disabled.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
