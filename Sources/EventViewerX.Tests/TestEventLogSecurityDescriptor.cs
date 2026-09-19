using Xunit;

namespace EventViewerX.Tests;

public class TestEventLogSecurityDescriptor {
    [Fact]
    public void ParsesEventLogRightsWithoutLocalizedAccountNames() {
        const string sddl = "O:BAG:SYD:(A;;0xf0007;;;SY)(A;;0x1;;;NS)(D;;0x2;;;BA)";

        IReadOnlyList<EventLogAccessRule> rules = EventLogSecurityDescriptor.ParseAccessRules(sddl);

        Assert.Collection(rules,
            system => {
                Assert.Equal("S-1-5-18", system.TrusteeSid);
                Assert.True(system.IsAllow);
                Assert.True(system.IncludesReadRight);
                Assert.True(system.IncludesWriteRight);
                Assert.True(system.IncludesClearRight);
            },
            networkService => {
                Assert.Equal("S-1-5-20", networkService.TrusteeSid);
                Assert.True(networkService.IsAllow);
                Assert.True(networkService.IncludesReadRight);
                Assert.False(networkService.IncludesWriteRight);
            },
            administrators => {
                Assert.Equal("S-1-5-32-544", administrators.TrusteeSid);
                Assert.True(administrators.IsDeny);
                Assert.False(administrators.IsAllow);
                Assert.True(administrators.IncludesWriteRight);
            });
    }

    [Fact]
    public void MalformedDescriptorCannotBeReportedAsAnEmptyAcl() {
        Assert.ThrowsAny<Exception>(() => EventLogSecurityDescriptor.ParseAccessRules("not-sddl"));
    }

    [Fact]
    public void LiveLogProvidesParsedRulesAlongsideRawDescriptor() {
        if (!OperatingSystem.IsWindows()) return;

        EventLogDetailsResult result = EventLogCatalog.GetLogDetailsResult("Application");

        Assert.NotNull(result.Details);
        Assert.False(string.IsNullOrWhiteSpace(result.Details.SecurityDescriptor));
        Assert.NotNull(result.Details.SecurityAccessRules);
        Assert.NotEmpty(result.Details.SecurityAccessRules);
    }
}
