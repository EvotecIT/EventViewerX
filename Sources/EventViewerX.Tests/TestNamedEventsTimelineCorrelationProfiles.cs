using EventViewerX.Reports.Correlation;
using Xunit;

namespace EventViewerX.Tests;

public class TestNamedEventsTimelineCorrelationProfiles {
    [Fact]
    public void Names_ShouldExposeKnownProfiles() {
        Assert.Equal(
            new[] { "actor_activity", "host_activity", "identity", "object_activity", "rule_activity" },
            NamedEventsTimelineCorrelationProfiles.Names);
    }

    [Fact]
    public void TryResolve_ShouldNormalizeProfileNameVariants() {
        var resolved = NamedEventsTimelineCorrelationProfiles.TryResolve(
            "ActorActivity",
            out var normalizedProfile,
            out var keys,
            out var error);

        Assert.True(resolved);
        Assert.Null(error);
        Assert.Equal("actor_activity", normalizedProfile);
        Assert.Equal(new[] { "who", "action", "computer" }, keys);
    }

    [Fact]
    public void TryResolve_ShouldReturnErrorForUnknownProfile() {
        var resolved = NamedEventsTimelineCorrelationProfiles.TryResolve(
            "not_a_profile",
            out _,
            out _,
            out var error);

        Assert.False(resolved);
        Assert.NotNull(error);
        Assert.Contains("Allowed values", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("___")]
    public void TryResolve_ShouldRejectTokensThatNormalizeToBlank(string rawProfile) {
        var resolved = NamedEventsTimelineCorrelationProfiles.TryResolve(
            rawProfile,
            out _,
            out _,
            out var error);

        Assert.False(resolved);
        Assert.NotNull(error);
        Assert.Contains("Allowed values", error, StringComparison.Ordinal);
    }
}
