using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventReportPresentationBounds {
    [Fact]
    public async Task EmailDoesNotFormatPayloadsOutsideItsDigestWindow() {
        var unselected = new UnselectedValue();
        var rows = new[] {
            new EventReportRow { Type = "Generic", Message = "selected-message", Values = new Dictionary<string, object?> { ["Value"] = "selected" } },
            new EventReportRow { Type = "Generic", Values = new Dictionary<string, object?> { ["Value"] = unselected } }
        };
        EventReport report = EventReportEngine.CreateStored(rows, new[] { EventReportSectionSchema.CreateGeneric() });
        unselected.FailWhenFormatted = true;
        EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report, maximumRows: 1);
        Assert.Contains("selected-message", email.Html, StringComparison.Ordinal);
        Assert.Contains("2 events", email.Subject, StringComparison.Ordinal);
        Assert.Equal(2, report.Rows.Count);
    }

    [Theory]
    [InlineData("ABC", "abc", true)]
    [InlineData("ABC", "different", false)]
    [InlineData("é", "É", true)]
    public void ColumnClassificationKeepsCaseInsensitiveConstancyAndIgnoresPlaceholders(string first, string second, bool constant) {
        EventReport report = EventReportEngine.CreateStored(new[] {
            new EventReportRow { Type = "Generic", Values = new Dictionary<string, object?> { ["Value"] = "---" } },
            new EventReportRow { Type = "Generic", Values = new Dictionary<string, object?> { ["Value"] = first } },
            new EventReportRow { Type = "Generic", Values = new Dictionary<string, object?> { ["Value"] = second } }
        }, new[] { EventReportSectionSchema.CreateGeneric() });
        EventReportPresentationSection presentation = EventReportPresentationProjection.Create(report.Sections[0]);
        Assert.Equal(constant, Assert.Single(presentation.Columns, static column => column.Name == "Value").IsConstant);
    }

    private sealed class UnselectedValue {
        internal bool FailWhenFormatted { get; set; }
        public override string ToString() => FailWhenFormatted
            ? throw new InvalidOperationException("A row outside the digest window was formatted.") : "unselected";
    }
}
