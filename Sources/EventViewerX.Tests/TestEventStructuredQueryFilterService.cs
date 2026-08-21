using System;
using System.Collections.Generic;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventStructuredQueryFilterService {
    [Fact]
    public void TryNormalize_ShouldRejectUnknownKeywords() {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                Keywords = "not_a_keyword"
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("keywords", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryNormalize_ShouldRejectTooManyEventIds() {
        var values = new List<int>();
        for (var i = 0; i < EventStructuredQueryFilterService.MaxEventIds + 1; i++) {
            values.Add(i + 1);
        }

        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                EventIds = values
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("event_ids", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildXPath_ShouldIncludeCoreStructuredFilters() {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                EventIds = new[] { 4624 },
                ProviderName = "Microsoft-Windows-Security-Auditing",
                Level = "error",
                UserId = "S-1-5-18",
                NamedDataFilter = new Dictionary<string, IReadOnlyList<string>> {
                    ["TargetUserName"] = new[] { "alice" }
                }
            },
            out var filter,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        var xpath = EventStructuredQueryFilterService.BuildXPath(filter);
        Assert.Contains("EventID=4624", xpath, StringComparison.Ordinal);
        Assert.Contains("Provider", xpath, StringComparison.Ordinal);
        Assert.Contains("TargetUserName", xpath, StringComparison.Ordinal);
    }

    [Fact]
    public void TryNormalize_ShouldRejectUnresolvableUserIds() {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                UserId = "not-a-sid-or-account"
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("user_id", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("none")]
    public void TryNormalize_ShouldTreatZeroKeywordMaskAsUnfiltered(string rawKeywords) {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                Keywords = rawKeywords
            },
            out var filter,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(filter);
        Assert.False(filter!.Keywords.HasValue);
    }

    [Fact]
    public void TryNormalize_ShouldPreserveCaseDistinctNamedDataValues() {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                NamedDataFilter = new Dictionary<string, IReadOnlyList<string>> {
                    ["TargetUserName"] = new[] { "Alice", "alice" }
                }
            },
            out var filter,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(filter);

        var values = Assert.IsType<string[]>(filter!.NamedDataFilter!["TargetUserName"]);
        Assert.Equal(new[] { "Alice", "alice" }, values);
    }

    [Fact]
    public void TryNormalize_ShouldPreserveCaseDistinctNamedDataKeys() {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                NamedDataFilter = new Dictionary<string, IReadOnlyList<string>> {
                    ["TargetUserName"] = new[] { "Alice" },
                    ["targetusername"] = new[] { "alice" }
                }
            },
            out var filter,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(filter);
        Assert.Equal(2, filter!.NamedDataFilter!.Count);
        Assert.Equal(new[] { "Alice" }, Assert.IsType<string[]>(filter.NamedDataFilter["TargetUserName"]));
        Assert.Equal(new[] { "alice" }, Assert.IsType<string[]>(filter.NamedDataFilter["targetusername"]));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData(" -1 ")]
    [InlineData("- 1")]
    [InlineData("--1")]
    [InlineData("-error")]
    public void TryNormalize_ShouldRejectNegativeLevelValues(string rawLevel) {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                Level = rawLevel
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("level", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("16", 16)]
    [InlineData("255", 255)]
    public void TryNormalize_ShouldAcceptCustomProviderLevels(
        string rawLevel,
        int expectedLevel) {

        bool ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                Level = rawLevel
            },
            out EventStructuredQueryFilter? filter,
            out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(expectedLevel, (int)filter!.Level!.Value);
        Assert.Contains(
            $"Level={expectedLevel}",
            EventStructuredQueryFilterService.BuildXPath(filter),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData(" -1 ")]
    [InlineData("- 1")]
    [InlineData("--1")]
    [InlineData("-audit_success")]
    public void TryNormalize_ShouldRejectNegativeKeywordMasks(string rawKeywords) {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                Keywords = rawKeywords
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("keywords", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryNormalize_ShouldRejectInvertedTimeRange() {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                StartTimeUtc = new DateTime(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc),
                EndTimeUtc = new DateTime(2026, 3, 17, 11, 0, 0, DateTimeKind.Utc)
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("start_time_utc", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryNormalize_ShouldRejectEmptyNamedDataExcludeList() {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                NamedDataExcludeFilter = new Dictionary<string, IReadOnlyList<string>> {
                    ["TargetUserName"] = Array.Empty<string>()
                }
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("named_data_exclude_filter", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryNormalize_ShouldRejectControlCharactersInNamedDataKeys() {
        var ok = EventStructuredQueryFilterService.TryNormalize(
            new EventStructuredQueryFilterInput {
                NamedDataFilter = new Dictionary<string, IReadOnlyList<string>> {
                    ["Target\u0001UserName"] = new[] { "alice" }
                }
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("named_data_filter", error, StringComparison.OrdinalIgnoreCase);
    }
}
