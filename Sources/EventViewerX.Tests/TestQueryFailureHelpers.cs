using System.ComponentModel;
using EventViewerX.Reports.QueryHelpers;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestQueryFailureHelpers {
    [Theory]
    [InlineData(15001, (int)NativeQueryFailureKind.InvalidQuery)]
    [InlineData(15007, (int)NativeQueryFailureKind.LogNotFound)]
    [InlineData(5, (int)NativeQueryFailureKind.AccessDenied)]
    [InlineData(1460, (int)NativeQueryFailureKind.Timeout)]
    [InlineData(1722, (int)NativeQueryFailureKind.HostUnavailable)]
    [InlineData(999, (int)NativeQueryFailureKind.Exception)]
    public void NativeQueryFailuresRetainTheirTypedCategory(
        int errorCode,
        int expected) {

        Assert.Equal(
            expected,
            (int)QueryFailureHelpers.Classify(
                new Win32Exception(
                    errorCode)));
    }
}
