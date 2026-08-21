using HtmlForgeX.Email;

namespace EventViewerX.Reporting;

/// <summary>Creates responsive event-report email payloads without choosing a mail transport.</summary>
public static class EventReportEmailRenderer {
    /// <summary>Renders a compact email digest for Mailozaurr or another transport.</summary>
    public static async Task<EventEmailPackage> RenderAsync(EventReport report, int maximumRows = 25) {
        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        if (maximumRows < 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumRows));
        }
        string subject = $"{report.Title} - {report.Rows.Count:N0} events";
        var email = new Email()
            .WithThemeMode(EmailThemeMode.Auto)
            .ConfigureLayout(containerPadding: "16px", contentPadding: "12px", maxWidth: "760px");
        email.Head.AddTitle(report.Title);
        email.Body.PreheaderText = $"{report.Rows.Count:N0} events across {report.Coverage.Count:N0} sources.";
        email.Body.EmailBox(box => {
            box.EmailHero(hero => hero
                .WithIcon("🛡️")
                .WithEyebrow("EventViewerX")
                .WithTitle(report.Title)
                .WithSubtitle($"{report.Rows.Count:N0} events · {report.QueryDuration.TotalSeconds:N2}s query"));
            box.EmailDivider().WithPattern(EmailDividerPattern.Solid);
            var metrics = new EmailGrid().WithColumns(3).WithGap(GridGap.Small).WithStackOnMobile(true);
            metrics.Add(new EmailMetricTile().WithIcon("📋").WithValue(report.Rows.Count.ToString("N0"), "Events"));
            metrics.Add(new EmailMetricTile().WithIcon("🖥️").WithValue(report.Coverage.Count.ToString("N0"), "Sources"));
            metrics.Add(new EmailMetricTile().WithIcon("⚠️").WithValue(report.Coverage.Count(static item => !item.Succeeded).ToString("N0"), "Failures"));
            box.Add(metrics);
            EventReportSection[] populatedSections = report.Sections
                .Where(static section => section.Rows.Count > 0)
                .ToArray();
            int rowsPerSection = populatedSections.Length == 0 ? 0 : maximumRows / populatedSections.Length;
            int extraRows = populatedSections.Length == 0 ? 0 : maximumRows % populatedSections.Length;
            for (int index = 0; index < populatedSections.Length; index++) {
                EventReportSection section = populatedSections[index];
                int allocation = rowsPerSection + (index < extraRows ? 1 : 0);
                int take = Math.Min(allocation, section.Rows.Count);
                if (take == 0) {
                    continue;
                }
                List<Dictionary<string, object?>> rows = ProjectEmailRows(section, take);
                box.EmailDivider().WithPattern(EmailDividerPattern.Dashed);
                box.EmailHeading(section.DisplayName, level: 3);
                box.EmailText($"{section.Rows.Count:N0} matching event{(section.Rows.Count == 1 ? string.Empty : "s")}");
                box.EmailTable(rows);
            }
        });
        EmailRenderResult result = await email.RenderAsync().ConfigureAwait(false);
        string plainText = $"{report.Title}{Environment.NewLine}{report.Rows.Count:N0} events across {report.Coverage.Count:N0} sources.{Environment.NewLine}Query duration: {report.QueryDuration.TotalSeconds:N2}s";
        return new EventEmailPackage(subject, plainText, result);
    }

    private static List<Dictionary<string, object?>> ProjectEmailRows(EventReportSection section, int maximumRows) {
        EventReportPresentationSection presentation = EventReportPresentationProjection.Create(section);
        EventReportPresentationColumn[] columns = presentation.PrimaryColumns
            .Take(5)
            .ToArray();
        return presentation.Rows.Take(maximumRows)
            .Select(row => columns.ToDictionary(column => column.DisplayName,
                column => row.TryGetValue(column.Name, out object? value) ? value : null,
                StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
}
