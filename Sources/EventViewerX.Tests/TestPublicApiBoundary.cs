using Xunit;
using EventViewerX.Storage;

namespace EventViewerX.Tests;

public class TestPublicApiBoundary {
    [Fact]
    public void ReportingImplementation_IsNotExported() {
        string[] exportedReportingTypes = typeof(EventLogEngine)
            .Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith("EventViewerX.Reports", StringComparison.Ordinal) == true)
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(exportedReportingTypes);
    }

    [Fact]
    public void CoreQueryEntryPoints_RemainExported() {
        Type[] supportedEntryPoints = {
            typeof(EventLogEngine),
            typeof(EventQueryDefinition),
            typeof(EventTypeEngine),
            typeof(EventTypeQuery),
            typeof(EventType),
            typeof(EventTypeCatalog),
            typeof(EventDefinition),
            typeof(EventDefinitionEngine),
            typeof(EventDefinitionCompiler)
        };

        Assert.All(supportedEntryPoints, type => Assert.True(type.IsPublic, type.FullName));
    }

    [Fact]
    public void AnalysisEntryPointsAreExportedFromTheCoreAssembly() {
        Type[] supportedEntryPoints = {
            typeof(EventViewerX.Reporting.EventReportEngine),
            typeof(EventViewerX.Reporting.EventReportRequest),
            typeof(EventViewerX.Reporting.EventAggregationEngine),
            typeof(EventViewerX.Reporting.EventOccurrenceEngine)
        };

        Assert.All(supportedEntryPoints, type => {
            Assert.True(type.IsPublic, type.FullName);
            Assert.Equal("EventViewerX", type.Assembly.GetName().Name);
        });
    }

    [Fact]
    public void PresentationEntryPointsAreExportedFromTheReportingAssembly() {
        Type[] supportedEntryPoints = {
            typeof(EventViewerX.Reporting.EventReportHtmlRenderer),
            typeof(EventViewerX.Reporting.EventReportExcelRenderer),
            typeof(EventViewerX.Reporting.EventReportEmailRenderer)
        };

        Assert.All(supportedEntryPoints, type => {
            Assert.True(type.IsPublic, type.FullName);
            Assert.Equal("EventViewerX.Reporting", type.Assembly.GetName().Name);
        });
    }

    [Fact]
    public void StorageAssemblyDoesNotReferenceReportingAssembly() {
        string[] references = typeof(EventStore).Assembly.GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("EventViewerX.Reporting", references);
        Assert.Contains("EventViewerX", references);
    }
}
