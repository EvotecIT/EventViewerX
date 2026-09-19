using Xunit;

namespace EventViewerX.Tests;

public class TestEventLogSecurityDescriptor {
    [Fact]
    public void ParsesEventLogRightsWithoutLocalizedAccountNames() {
        const string sddl = "O:BAG:SYD:(A;;0xf0007;;;SY)(A;;0x1;;;NS)(D;;0x2;;;BA)";

        EventLogParsedSecurityDescriptor parsed = EventLogSecurityDescriptor.Parse(sddl);
        Assert.Equal(SecurityDescriptorDaclState.Entries, parsed.DaclState);
        IReadOnlyList<EventLogAccessRule> rules = parsed.AccessRules;

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
        Assert.ThrowsAny<Exception>(() => EventLogSecurityDescriptor.Parse("not-sddl"));

        var policy = new ChannelPolicy { SecurityDescriptor = "not-sddl" };
        Assert.Equal(SecurityDescriptorDaclState.Unavailable, policy.Security.DaclState);
        Assert.Null(policy.Security.AccessRules);
        Assert.False(string.IsNullOrWhiteSpace(policy.Security.Diagnostic));
    }

    [Fact]
    public void ChannelPolicyInspectionTracksEditedDescriptorWithoutStaleRules() {
        var policy = new ChannelPolicy {
            SecurityDescriptor = "O:SYG:SYD:(A;;0x1;;;SY)"
        };

        Assert.Equal(SecurityDescriptorDaclState.Entries, policy.Security.DaclState);
        Assert.Single(policy.Security.AccessRules!);

        policy.SecurityDescriptor = "O:SYG:SYD:";

        Assert.Equal(SecurityDescriptorDaclState.Empty, policy.Security.DaclState);
        Assert.Empty(policy.Security.AccessRules!);
        Assert.Equal(SecurityDescriptorDaclState.Empty,
            Assert.IsType<EventLogChannelSecurityInfo>(policy.ToDictionary()[nameof(ChannelPolicy.Security)]).DaclState);
    }

    [Theory]
    [InlineData("O:SYG:SY", SecurityDescriptorDaclState.NotPresent)]
    [InlineData("O:SYG:SYD:NO_ACCESS_CONTROL", SecurityDescriptorDaclState.Null)]
    [InlineData("O:SYG:SYD:", SecurityDescriptorDaclState.Empty)]
    public void DistinguishesMissingNullAndEmptyDacls(string sddl, SecurityDescriptorDaclState expectedState) {
        EventLogParsedSecurityDescriptor parsed = EventLogSecurityDescriptor.Parse(sddl);

        Assert.Equal(expectedState, parsed.DaclState);
        Assert.Empty(parsed.AccessRules);
    }

    [Fact]
    public void LiveLogProvidesParsedRulesAlongsideRawDescriptor() {
        if (!OperatingSystem.IsWindows()) return;

        EventLogDetailsResult result = EventLogCatalog.GetLogDetailsResult("Application");

        Assert.NotNull(result.Details);
        Assert.False(string.IsNullOrWhiteSpace(result.Details.SecurityDescriptor));
        Assert.NotNull(result.Details.SecurityAccessRules);
        Assert.Equal(SecurityDescriptorDaclState.Entries, result.Details.SecurityDaclState);
        Assert.NotEmpty(result.Details.SecurityAccessRules);

        ChannelPolicy policy = EventLogChannelPolicyService.Get("Application") ??
            throw new InvalidOperationException("Application channel policy was unavailable.");
        Assert.False(string.IsNullOrWhiteSpace(policy.SecurityDescriptor));
        Assert.Equal(SecurityDescriptorDaclState.Entries, policy.Security.DaclState);
        Assert.NotEmpty(policy.Security.AccessRules!);
    }

    [Fact]
    public void EditedLogDetailsNeverRetainRulesFromAPreviousDescriptor() {
        if (!OperatingSystem.IsWindows()) return;

        EventLogDetailsResult result = EventLogCatalog.GetLogDetailsResult("Application");
        EventLogDetails details = Assert.IsType<EventLogDetails>(result.Details);

        details.SecurityDescriptor = "O:SYG:SYD:(A;;0x1;;;SY)";
        Assert.Equal(SecurityDescriptorDaclState.Entries, details.SecurityDaclState);
        Assert.Equal("S-1-5-18", Assert.Single(details.SecurityAccessRules!).TrusteeSid);

        details.SecurityDescriptor = "O:SYG:SYD:";
        Assert.Equal(SecurityDescriptorDaclState.Empty, details.SecurityDaclState);
        Assert.Empty(details.SecurityAccessRules!);

        details.SecurityDescriptor = "not-sddl";
        Assert.Equal(SecurityDescriptorDaclState.Unavailable, details.SecurityDaclState);
        Assert.Null(details.SecurityAccessRules);
        Assert.False(string.IsNullOrWhiteSpace(details.Security.Diagnostic));
        Assert.DoesNotContain(details.Diagnostics,
            diagnostic => diagnostic.PropertyName == nameof(EventLogDetails.SecurityAccessRules));
    }
}
