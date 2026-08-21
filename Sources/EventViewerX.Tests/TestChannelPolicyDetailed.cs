using System.Linq;
using System.Diagnostics.Eventing.Reader;
using Xunit;

namespace EventViewerX.Tests;

public class TestChannelPolicyDetailed
{
    [Fact]
    public void ChannelPolicy_TrySetModeName_UsesCanonicalModeNames()
    {
        var policy = new ChannelPolicy();

        var success = policy.TrySetModeName("AUTO_BACKUP", out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("auto_backup", policy.ModeName);
    }

    [Fact]
    public void ChannelPolicy_TrySetModeName_RejectsUnknownValues()
    {
        var policy = new ChannelPolicy();

        var success = policy.TrySetModeName("archive_forever", out var error);

        Assert.False(success);
        Assert.Equal("mode must be one of: circular, retain, auto_backup.", error);
        Assert.Null(policy.ModeName);
    }

    [Fact]
    public void GetChannelPolicyRejectsAnUnboundedCatalogTimeout()
    {
        var query = new EventLogCatalogQuery {
            ConnectionTimeoutMilliseconds = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogChannelPolicyService.Get(
                "Application",
                query));
    }

    [Fact]
    public void GetChannelPoliciesValidateTheQueryBeforeReturningTheStream()
    {
        var query = new EventLogCatalogQuery {
            ConnectionTimeoutMilliseconds = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogChannelPolicyService.GetMany(
                query));
    }

    [Fact]
    public void GetChannelPolicies_ParallelEnumerates()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Parallel enumeration should not throw and produce items when available
        var items = EventLogChannelPolicyService
            .GetMany(machineName: null, includePatterns: new[] { "*" }, parallel: true, degreeOfParallelism: 2)
            .Take(5)
            .ToList();

        Assert.NotNull(items);
        foreach (var p in items)
        {
            Assert.NotNull(p);
            Assert.False(string.IsNullOrWhiteSpace(p.LogName));
        }
    }

    [Fact]
    public void SetChannelPolicyDetailed_UnchangedValueReportsTruthfully()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ChannelPolicy existing =
            EventLogChannelPolicyService.Get(
                "Application") ??
            throw new InvalidOperationException(
                "Application log policy was unavailable.");
        var result =
            EventLogChannelPolicyService.ApplyDetailed(
                new ChannelPolicy {
                    LogName = "Application",
                    MaximumSizeInBytes =
                        existing.MaximumSizeInBytes
                });

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.False(result.PartialSuccess);
        Assert.False(result.Changed);
        Assert.Contains(
            "MaximumSizeInBytes",
            result.RequestedProperties);
        Assert.Contains(
            "MaximumSizeInBytes",
            result.UnchangedProperties);
        Assert.Empty(result.AppliedProperties);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Before);
        Assert.NotNull(result.After);
    }

    [Fact]
    public void SavedPolicyResultWinsOverPostSaveCancellation() {
        using var cancellation =
            new CancellationTokenSource();
        var applied = new List<string>();

        EventLogChannelPolicyService.PersistChanges(
            () => cancellation.Cancel(),
            new[] { "MaximumSizeInBytes" },
            applied,
            cancellation.Token);

        Assert.Equal(
            new[] { "MaximumSizeInBytes" },
            applied);
    }
}
