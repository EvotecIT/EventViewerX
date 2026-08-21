using Xunit;

namespace EventViewerX.Tests;

public class TestEventLogStructuredQuery {
    [Fact]
    public void CountsDistinctOfflineFilesAsIndependentSources() {
        string first = Path.GetFullPath("first.evtx");
        string second = Path.GetFullPath("second.evtx");
        EventLogStructuredQuery query =
            EventLogStructuredQuery.ForFiles(
                new[] {
                    first,
                    second
                });

        Assert.Equal(
            2,
            query.GetIndependentSourceCount());
    }

    [Fact]
    public void CountsMultipleChannelsAsOneNativeSource() {
        EventLogStructuredQuery query =
            EventLogStructuredQuery.ForChannels(
                new[] {
                    "Application",
                    "System"
                });

        Assert.Equal(
            1,
            query.GetIndependentSourceCount());
    }

    [Fact]
    public void MultiSourceStructuredReadRejectsOneBookmarkBeforeSplitting() {
        EventLogStructuredQuery query =
            EventLogStructuredQuery.ForFiles(
                new[] {
                    Path.GetFullPath("first.evtx"),
                    Path.GetFullPath("second.evtx")
                });
        query.BookmarkXml = "<BookmarkList />";

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EventLogEngine
                    .ReadStructured(query)
                    .ToArray());

        Assert.Contains(
            "one independent structured-query source",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfflineFileSourcesEscapeReservedUriCharacters() {
        string path = Path.GetFullPath(
            Path.Combine(
                "logs",
                "a#b?c%20.evtx"));
        EventLogStructuredQuery query =
            EventLogStructuredQuery.ForFiles(
                new[] { path });
        EventLogStructuredQuerySource source =
            Assert.Single(
                query.ResolveSources());

        Assert.Equal(
            path,
            source.Source);
        Assert.Contains(
            "%23",
            query.QueryXml,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "%3F",
            query.QueryXml,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "%2520",
            query.QueryXml,
            StringComparison.OrdinalIgnoreCase);
    }
}
