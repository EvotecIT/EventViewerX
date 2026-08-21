using Xunit;

namespace EventViewerX.Tests;

/// <summary>Marks a test that requires explicit access to a live Event Viewer environment.</summary>
internal sealed class LiveFactAttribute : FactAttribute {
    public LiveFactAttribute() {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("EVENTVIEWERX_RUN_LIVE_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase)) {
            Skip = "Set EVENTVIEWERX_RUN_LIVE_TESTS=true and provide the documented lab access to run this live test.";
        }
    }
}
