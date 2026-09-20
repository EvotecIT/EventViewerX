using EventViewerX.Sigma;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestSigmaTelemetryProfiles {
    private const string ProcessCreationRule = """
        title: Process creation profile test
        id: aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa
        logsource:
          product: windows
          category: process_creation
        detection:
          selection:
            Image|endswith: '\\powershell.exe'
          condition: selection
        """;

    [Fact]
    public void CategoryWithoutEventIdRemainsUnsupportedByDefault() {
        SigmaCompilationResult result = SigmaRuleCompiler.CompileYaml(ProcessCreationRule);

        Assert.False(result.IsSupported);
        SigmaDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EVXSIGMA023", diagnostic.Code);
        Assert.Empty(result.Rules);
    }

    [Fact]
    public void ExplicitWindowsProfileMapsCategoryToConcreteTelemetry() {
        var options = new SigmaCompilationOptions {
            LogSourceProfile = SigmaLogSourceProfile.WindowsSysmonAndPowerShell
        };

        SigmaCompilationResult result = SigmaRuleCompiler.CompileYaml(ProcessCreationRule, options);
        EventDetectionRuleDefinition definition = Assert.Single(result.Rules).Definition;

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new[] { "Microsoft-Windows-Sysmon/Operational" }, definition.Channels);
        Assert.Equal(new[] { "Microsoft-Windows-Sysmon" }, definition.Providers);
        Assert.Equal(new[] { 1 }, definition.EventIds);
    }

    [Fact]
    public void ExplicitEventIdRemainsAuthoritativeWhenProfileIsSelected() {
        const string yaml = """
            title: Explicit event ID category test
            id: bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb
            logsource:
              product: windows
              category: process_creation
            detection:
              selection:
                EventID: 4688
                NewProcessName|endswith: '\\powershell.exe'
              condition: selection
            """;
        var options = new SigmaCompilationOptions {
            LogSourceProfile = SigmaLogSourceProfile.WindowsSysmonAndPowerShell
        };

        SigmaCompilationResult result = SigmaRuleCompiler.CompileYaml(yaml, options);
        EventDetectionRuleDefinition definition = Assert.Single(result.Rules).Definition;

        Assert.True(result.IsSupported);
        Assert.Empty(definition.Channels);
        Assert.Empty(definition.Providers);
        Assert.Equal(new[] { 4688 }, definition.EventIds);
    }

    [Fact]
    public void ConflictingServiceAndProfileCategoryAreRejected() {
        const string yaml = """
            title: Conflicting telemetry test
            id: cccccccc-cccc-4ccc-8ccc-cccccccccccc
            logsource:
              product: windows
              service: security
              category: process_creation
            detection:
              selection:
                Image|endswith: '\\powershell.exe'
              condition: selection
            """;
        var options = new SigmaCompilationOptions {
            LogSourceProfile = SigmaLogSourceProfile.WindowsSysmonAndPowerShell
        };

        SigmaCompilationResult result = SigmaRuleCompiler.CompileYaml(yaml, options);

        Assert.False(result.IsSupported);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "EVXSIGMA024");
        Assert.Empty(result.Rules);
    }

    [Fact]
    public void BuiltInProfileIsVersionedAndHasUniqueMappings() {
        SigmaLogSourceProfile profile = SigmaLogSourceProfile.WindowsSysmonAndPowerShell;

        Assert.Equal("windows-sysmon-powershell", profile.ProfileId);
        Assert.Equal("1.0.0", profile.Version);
        Assert.Equal(
            profile.Mappings.Count,
            profile.Mappings.Select(static mapping => mapping.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.All(profile.Mappings, static mapping => {
            Assert.NotEmpty(mapping.Channels);
            Assert.NotEmpty(mapping.EventIds);
        });
    }
}
