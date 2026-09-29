using Xunit;
using EventViewerX.Storage;

namespace EventViewerX.Tests;

public class TestPublicApiBoundary {
    [Fact]
    public void ReportingExecutorsRemainInternalWhileQueryFacadesAreExported() {
        Type[] exportedReportingTypes = typeof(EventLogEngine)
            .Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith("EventViewerX.Reports", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Contains(typeof(EventViewerX.Reports.Live.LiveEventReportEngine), exportedReportingTypes);
        Assert.Contains(typeof(EventViewerX.Reports.Inventory.EventCatalogReportEngine), exportedReportingTypes);
        Assert.Contains(typeof(EventViewerX.Reports.Stats.EvtxStatisticsEngine), exportedReportingTypes);
        Assert.Contains(typeof(EventViewerX.Reports.Security.SecurityEvtxSummaryEngine), exportedReportingTypes);
        Assert.Contains(typeof(EventViewerX.Reports.Correlation.EventTypeCorrelationEngine), exportedReportingTypes);
        Assert.DoesNotContain(exportedReportingTypes, type => type.Name.EndsWith("Executor", StringComparison.Ordinal));
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
