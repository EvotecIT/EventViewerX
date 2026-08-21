using System.Reflection;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventLogDetails {
    [Fact]
    public void ConvertSizeHandlesNonPositiveValues() {
        var method = typeof(EventViewerX.EventLogDetails).GetMethod("ConvertSize", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        double negative = (double)method!.Invoke(null, new object?[] { (double?)-1, "B", "MB", 2 })!;
        Assert.Equal(0, negative);

        double zero = (double)method.Invoke(null, new object?[] { (double?)0, "B", "MB", 2 })!;
        Assert.Equal(0, zero);

        double positive = (double)method.Invoke(null, new object?[] { (double?)1048576, "B", "MB", 2 })!;
        Assert.Equal(1, positive);
    }
}
