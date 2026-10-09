using Xunit;

namespace EventViewerX.Tests;

/// <summary>Marks native Windows contracts that require an elevated administrator process.</summary>
internal sealed class WindowsAdministratorTheoryAttribute : TheoryAttribute {
    public WindowsAdministratorTheoryAttribute() {
        if (!OperatingSystem.IsWindows()) {
            Skip = "This native contract requires Windows.";
        } else if (!TestEnv.IsAdmin()) {
            Skip = "Windows provider-resource archiving requires an elevated test process. Run these cases as administrator.";
        }
    }
}