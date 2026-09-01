using Xunit;

namespace EventViewerX.Portability.Tests;

public sealed class TestPathInputPortability {
    [Fact]
    public void FileQueryBuilderPreservesCaseDistinctUnresolvedPatterns() {
        var builder = new EventQueryDefinitionBuilder();
        builder.FromFiles(
            "Logs/A*.evtx",
            "Logs/a*.evtx",
            " Logs/A*.evtx ");

        EventQueryDefinition query = builder.Build();

        Assert.Equal(
            new[] { "Logs/A*.evtx", "Logs/a*.evtx" },
            query.Paths);
    }

    [Fact]
    public void QueryPlannerExpandsWildcardDirectorySegmentsPortably() {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-PortablePaths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            string first = Directory.CreateDirectory(
                Path.Combine(directory, "Archive-One")).FullName;
            string second = Directory.CreateDirectory(
                Path.Combine(directory, "Archive-Two")).FullName;
            File.WriteAllText(Path.Combine(first, "one.evtx"), string.Empty);
            File.WriteAllText(Path.Combine(second, "two.evtx"), string.Empty);

            EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
                new EventQueryDefinition {
                    Paths = new[] { Path.Combine(directory, "Archive-*", "*.evtx") }
                });

            Assert.Equal(2, batch.StructuredQueries.Count);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
