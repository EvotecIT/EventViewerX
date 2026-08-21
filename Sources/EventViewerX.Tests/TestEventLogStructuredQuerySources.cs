using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventLogStructuredQuerySources {
    [Fact]
    public void ChildPathsOverrideUnusedQueryDefaultPath() {
        const string xml =
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"file://C:/unused.evtx\">" +
            "<Select Path=\"file://C:/actual.evtx\">*</Select>" +
            "<Suppress Path=\"file://C:/actual.evtx\">*[System[Level=0]]</Suppress>" +
            "</Query>" +
            "</QueryList>";
        var query = new EventLogStructuredQuery(xml);

        EventLogStructuredQuerySource source =
            Assert.Single(query.ResolveSources());

        Assert.Equal(
            EventLogQuerySourceKind.File,
            source.Kind);
        Assert.Equal(
            Path.GetFullPath("C:/actual.evtx"),
            source.Source,
            ignoreCase: true);
        Assert.Equal(
            1,
            query.GetIndependentSourceCount());
    }

    [Fact]
    public void AddsSourceSpecificRecordIdSuppressions() {
        const string xml =
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"System\"><Select Path=\"System\">*</Select></Query>" +
            "<Query Id=\"1\" Path=\"Application\"><Select Path=\"Application\">*</Select></Query>" +
            "</QueryList>";
        var query = new EventLogStructuredQuery(xml);

        EventLogStructuredQuery bounded =
            query.WithMinimumRecordIdExclusive(source =>
                source.Source == "System" ? 41 : 73);

        Assert.Contains(
            "<Suppress Path=\"System\">*[System[EventRecordID &lt;= 41]]</Suppress>",
            bounded.QueryXml);
        Assert.Contains(
            "<Suppress Path=\"Application\">*[System[EventRecordID &lt;= 73]]</Suppress>",
            bounded.QueryXml);
        Assert.Equal(
            query.SourceKind,
            bounded.SourceKind);
    }

    [Fact]
    public void ResolvesDistinctCheckpointSources() {
        string filePath = Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "structured-source.evtx"));
        string fileUri = new Uri(filePath).AbsoluteUri;
        var query = new EventLogStructuredQuery(
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"System\">" +
            "<Select Path=\"System\">*</Select>" +
            "<Suppress Path=\"Application\">*</Suppress>" +
            "</Query>" +
            $"<Query Id=\"1\" Path=\"{fileUri}\">" +
            $"<Select Path=\"{fileUri}\">*</Select>" +
            "</Query>" +
            "</QueryList>");

        EventLogStructuredQuerySource[] sources =
            query.ResolveSources().ToArray();

        Assert.Equal(3, sources.Length);
        Assert.Contains(
            sources,
            source => source.Kind ==
                      EventLogQuerySourceKind.Channel &&
                      source.Source == "System");
        Assert.Contains(
            sources,
            source => source.Kind ==
                      EventLogQuerySourceKind.Channel &&
                      source.Source == "Application");
        Assert.Contains(
            sources,
            source => source.Kind ==
                      EventLogQuerySourceKind.File &&
                      string.Equals(
                          source.Source,
                          filePath,
                          StringComparison.OrdinalIgnoreCase));
    }
}
