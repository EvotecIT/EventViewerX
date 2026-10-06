using EventViewerX.Sigma;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestSigmaDetection {
    [Theory]
    [InlineData("|contains", "alpha*omega", "prefix alpha middle omega suffix", true)]
    [InlineData("|startswith", "alpha*omega", "alpha middle omega suffix", true)]
    [InlineData("|startswith", "alpha*omega", "prefix alpha middle omega", false)]
    [InlineData("|endswith", "alpha*omega", "prefix alpha middle omega", true)]
    [InlineData("|endswith", "alpha*omega", "alpha middle omega suffix", false)]
    [InlineData("", @"alpha\*omega", "alpha*omega", true)]
    [InlineData("", @"alpha\*omega", "alpha middle omega", false)]
    [InlineData("", "alpha?omega", "alpha\nomega", true)]
    [InlineData("", "`alpha*omega", "`alpha middle omega", true)]
    [InlineData("|re", "^ALPHA$", "alpha", false)]
    [InlineData("|re", "^ALPHA$", "ALPHA", true)]
    [InlineData("|contains|cased", "ALPHA*omega", "alpha middle omega", false)]
    public void SigmaStringsPreserveWildcardEscapeAndCaseSemantics(string modifier, string value, string actual, bool matched) {
        string yaml = $"""
            title: Sigma string contract
            id: string-contract
            logsource:
              product: windows
              service: security
            detection:
              selection:
                EventID: 4624
                CommandLine{modifier}: '{value}'
              condition: selection
            """;
        SigmaCompilationResult compilation = SigmaRuleCompiler.CompileYaml(yaml);
        Assert.True(compilation.IsSupported);
        EventObject source = CreateEvent(4624, 1);
        source.Data["CommandLine"] = actual;
        EventDetectionExecutionResult result = EventDetectionEngine.Evaluate(new[] { source }, compilation.CompilePlan());
        Assert.Equal(matched ? 1 : 0, result.Findings.Count);
    }
}
