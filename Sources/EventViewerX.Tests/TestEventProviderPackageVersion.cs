using EventViewerX.Providers;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventProviderPackageVersion {
    [Theory]
    [InlineData(
        "1.0.0-10000000000",
        "1.0.0-9999999999",
        1)]
    [InlineData(
        "1.0.0-9999999999",
        "1.0.0-10000000000",
        -1)]
    [InlineData(
        "10000000000.0.0",
        "9999999999.0.0",
        1)]
    [InlineData(
        "1.0.0-10000000000",
        "1.0.0-alpha",
        -1)]
    public void ComparesArbitrarilyLargeNumericIdentifiers(
        string left,
        string right,
        int expectedSign) {

        int comparison =
            EventProviderPackageVersion.Parse(left)
                .CompareTo(
                    EventProviderPackageVersion.Parse(right));

        Assert.Equal(
            expectedSign,
            Math.Sign(comparison));
    }

    [Fact]
    public void RejectsNonAsciiSemVerIdentifiers() {
        Assert.Throws<FormatException>(() =>
            EventProviderPackageVersion.Parse(
                "1.0.0-prérelease"));
    }
}
