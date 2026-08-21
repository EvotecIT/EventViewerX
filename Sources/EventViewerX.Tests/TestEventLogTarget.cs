using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventLogTarget {
    [Theory]
    [InlineData("HOST", null, "HOST")]
    [InlineData("HOST", "", "HOST")]
    [InlineData("HOST", "ad.example.test", "HOST.ad.example.test")]
    [InlineData("HOST.ad.example.test", "ad.example.test", "HOST.ad.example.test")]
    [InlineData("HOST.", ".ad.example.test.", "HOST.ad.example.test")]
    public void LocalIdentityUsesOnlyTheConfiguredMachineAndDomain(
        string machineName,
        string? domainName,
        string expected) {

        Assert.Equal(
            expected,
            EventLogTarget.BuildLocalMachineName(
                machineName,
                domainName));
    }

    [Fact]
    public void LocalClassificationAcceptsCanonicalTrailingDots() {
        Assert.True(
            EventLogTarget.IsLocalMachine("."));
        Assert.True(
            EventLogTarget.IsLocalMachine(
                Environment.MachineName + "."));
        Assert.True(
            EventLogTarget.IsLocalMachine(
                EventLogTarget.LocalMachineName + "."));
    }
}
