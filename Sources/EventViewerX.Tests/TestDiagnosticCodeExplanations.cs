using Xunit;

namespace EventViewerX.Tests;

public sealed class TestDiagnosticCodeExplanations {
    [Theory]
    [InlineData("5", EventDiagnosticCodeKind.Win32, "ERROR_ACCESS_DENIED", "access", "permissions")]
    [InlineData("0x80070005", EventDiagnosticCodeKind.HResult, "HRESULT_FROM_WIN32(ERROR_ACCESS_DENIED)", "embedded Win32", "permissions")]
    [InlineData("32", EventDiagnosticCodeKind.Win32, "ERROR_SHARING_VIOLATION", "handle", "sharing mode")]
    [InlineData("126", EventDiagnosticCodeKind.Win32, "ERROR_MOD_NOT_FOUND", "module", "dependencies")]
    [InlineData("267", EventDiagnosticCodeKind.Win32, "ERROR_DIRECTORY", "directory", "prerequisites")]
    [InlineData("1603", EventDiagnosticCodeKind.WindowsInstaller, "ERROR_INSTALL_FAILURE", "without identifying", "failing action")]
    [InlineData("1612", EventDiagnosticCodeKind.WindowsInstaller, "ERROR_INSTALL_SOURCE_ABSENT", "source", "repair")]
    [InlineData("1625", EventDiagnosticCodeKind.WindowsInstaller, "ERROR_INSTALL_PACKAGE_REJECTED", "policy", "policy")]
    [InlineData("1638", EventDiagnosticCodeKind.WindowsInstaller, "ERROR_PRODUCT_VERSION", "version", "upgrade")]
    [InlineData("0x80004005", EventDiagnosticCodeKind.HResult, "E_FAIL", "unspecified", "preceding")]
    public void DocumentedMeaningIncludesContextSpecificEvidenceToInspect(string text, EventDiagnosticCodeKind kind,
        string symbol, string explanation, string nextCheck) {
        EventDiagnosticCode code = EventDiagnosticCode.Resolve(text, kind);
        Assert.Equal(symbol, code.SymbolicName);
        Assert.Contains(explanation, code.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nextCheck, code.NextCheck, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://learn.microsoft.com/", code.Reference, StringComparison.Ordinal);
        Assert.Equal("Failure", code.Outcome);
    }

    [Theory]
    [InlineData("5", EventDiagnosticCodeKind.ProcessExit)]
    [InlineData("1603", EventDiagnosticCodeKind.Unknown)]
    [InlineData("3010", EventDiagnosticCodeKind.ProcessExit)]
    [InlineData("0x80070005", EventDiagnosticCodeKind.Unknown)]
    public void KnownBitsDoNotBorrowAnotherFamiliesMeaning(string text, EventDiagnosticCodeKind kind) {
        EventDiagnosticCode code = EventDiagnosticCode.Resolve(text, kind);
        Assert.Null(code.SymbolicName);
        Assert.Equal("Unknown", code.Outcome);
        Assert.Contains("mapping", code.NextCheck);
    }

    [Theory]
    [InlineData("0", EventDiagnosticCodeKind.Win32, "Success", "ERROR_SUCCESS")]
    [InlineData("1", EventDiagnosticCodeKind.HResult, "Success", "S_FALSE")]
    [InlineData("1641", EventDiagnosticCodeKind.WindowsInstaller, "SuccessRestartInitiated", "ERROR_SUCCESS_REBOOT_INITIATED")]
    [InlineData("3010", EventDiagnosticCodeKind.WindowsInstaller, "SuccessRestartRequired", "ERROR_SUCCESS_REBOOT_REQUIRED")]
    public void ExplanationsPreserveApiSuccessAndRestartContracts(string text, EventDiagnosticCodeKind kind, string outcome, string symbol) {
        EventDiagnosticCode code = EventDiagnosticCode.Resolve(text, kind);
        Assert.Equal(outcome, code.Outcome);
        Assert.Equal(symbol, code.SymbolicName);
        Assert.NotEmpty(code.NextCheck);
    }

    [Fact]
    public void UncataloguedHResultRetainsItsDeclaredBitLayoutWithoutInventedMeaning() {
        EventDiagnosticCode code = EventDiagnosticCode.Resolve("0x81234567", EventDiagnosticCodeKind.HResult);
        Assert.Equal((uint)0x81234567, code.Value);
        Assert.True(code.HResultFailure);
        Assert.Null(code.SymbolicName);
        Assert.Null(code.Win32Value);
        Assert.Equal("Failure", code.Outcome);
        Assert.Contains("not a root-cause", code.Explanation);
    }

    [Fact]
    public void FailureWithZeroWin32BitsDoesNotClaimTheSuccessMacro() {
        EventDiagnosticCode code = EventDiagnosticCode.Resolve("0x80070000", EventDiagnosticCodeKind.HResult);
        Assert.Equal("Failure", code.Outcome);
        Assert.Equal((uint)0, code.Win32Value);
        Assert.Null(code.SymbolicName);
        Assert.DoesNotContain("returned success", code.Explanation);
    }
}
