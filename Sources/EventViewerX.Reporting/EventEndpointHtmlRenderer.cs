using HtmlForgeX;

namespace EventViewerX.Reporting;

/// <summary>Presentation-only endpoint investigation report using shared HtmlForgeX diagnostic components.</summary>
public static class EventEndpointHtmlRenderer {
    /// <summary>Renders searchable conclusions, next checks, and evidence coordinates. Raw messages and identity fields require explicit inclusion.</summary>
    /// <remarks>Even without raw evidence, the report can contain application identifiers. Treat captured sessions and diagnostic exports as sensitive.</remarks>
    public static string Render(EventEndpointAnalysis analysis, bool includeSensitiveEvidence = false) {
        if (analysis == null) { throw new ArgumentNullException(nameof(analysis)); }
        using var document = new Document { LibraryMode = LibraryMode.Offline, ThemeMode = ThemeMode.System, DarkThemeVariant = HfxThemeVariant.DarkCarbon };
        document.Head.Title = "Endpoint investigation";
        var dashboard = new MonitoringDashboard().Brand("EventViewerX").FooterInfo("Captured evidence · no live collection or endpoint changes")
            .Settings(settings => settings.State(state => state.StateId("eventviewerx-endpoint").HashMode(MonitoringDashboardHashMode.Namespaced).End())
                .Theme(theme => theme.Selector().End()).End());
        dashboard.AddPage("findings", "Findings and next checks", "Conclusions bounded by supplied evidence", TablerIconType.ReportAnalytics, page => {
            page.AddMetric(metric => metric.Title("Input parsing").Value(analysis.InputComplete ? "Complete" : "Incomplete")
                .State(analysis.InputComplete ? MonitoringHealthState.Healthy : MonitoringHealthState.Warning)
                .Description("Describes supplied bytes, not endpoint or lifecycle coverage"));
            page.AddMetric(metric => metric.Title("Application attempts").Value(analysis.Applications.Length.ToString()).Description("Explicitly attributable records"));
            var explorer = Explorer("Diagnostic findings", "Rule", "Title", "Status");
            var evidence = analysis.Records.ToDictionary(record => record.Identity, StringComparer.Ordinal);
            for (int index = 0; index < analysis.Findings.Length; index++) {
                EventDiagnosticFinding finding = analysis.Findings[index];
                explorer.AddRecord("finding-" + index, finding.Title, record => {
                    record.Cell("Rule", finding.RuleId).Cell("Title", finding.Title).Cell("Status", finding.Status).Detail("Explanation", finding.Explanation)
                        .Detail("Next checks", string.Join("\n", finding.NextChecks.Select(check => check.Artifact + ": " + check.Action + " Reason: " + check.Reason)))
                        .Detail("Evidence", string.Join("\n", finding.EvidenceIdentities.Select(id => evidence.TryGetValue(id, out EventDiagnosticRecord? item)
                            ? item.Source + ": lines " + item.LineStart + "-" + item.LineEnd + ", bytes " + item.ByteStart + "-" + item.ByteEnd + " [" + id + "]" : "Artifact SHA-256: " + id)))
                        .Detail("Rule version", finding.RuleVersion).Detail("References", string.Join("\n", finding.References)).Tag(finding.Status);
                });
            }
            page.Panel("Findings", panel => panel.Subtitle("Search by application or status; select a record for full guidance and provenance").Content(explorer));
            var coverage = Explorer("Coverage limits", "Diagnostic");
            for (int index = 0; index < analysis.CoverageDiagnostics.Length; index++) {
                string diagnostic = analysis.CoverageDiagnostics[index];
                coverage.AddRecord("coverage-" + index, "Evidence limit", record => record.Cell("Diagnostic", diagnostic));
            }
            page.Panel("Coverage and uncertainty", panel => panel.Content(coverage));
        }, active: true);
        dashboard.AddPage("evidence", "Evidence", includeSensitiveEvidence ? "Sensitive captured records included" : "Coordinates only; raw evidence stays in the original session", TablerIconType.ListDetails, page => {
            var explorer = Explorer("Captured log records", "Source", "Lines", "Bytes", "Time", "Time quality", "Format", "Component", "Severity");
            if (includeSensitiveEvidence) { explorer.AddColumn("Message", "Message"); }
            foreach (EventDiagnosticRecord item in analysis.Records) {
                explorer.AddRecord(item.Identity, item.Source + ":" + item.LineStart, record => {
                    record.Cell("Source", item.Source).Cell("Lines", item.LineStart + "-" + item.LineEnd).Cell("Bytes", item.ByteStart + "-" + item.ByteEnd)
                        .Cell("Time", item.Timestamp?.ToString("O") ?? "Unknown instant").Cell("Time quality", item.TimeQuality).Cell("Format", item.Format)
                        .Cell("Component", item.Component).Cell("Severity", item.Severity?.ToString() ?? "Unknown").Detail("Identity", item.Identity)
                        .Detail("Diagnostic", item.Diagnostic ?? "None");
                    if (includeSensitiveEvidence) { record.Cell("Message", item.Message).Detail("Raw frame", item.RawText).Detail("Writer context", item.Context); }
                });
            }
            page.Panel("Records", panel => panel.Subtitle("Known instants use the writer's offset; unknown local times remain unknown").Content(explorer));
            if (includeSensitiveEvidence) {
                var identity = Explorer("Captured DSRegCmd fields", "Artifact", "Section", "Name", "Value", "Line", "Context", "Captured at");
                int index = 0;
                foreach (EventDsRegSnapshot snapshot in analysis.IdentitySnapshots) {
                    foreach (EventDsRegField field in snapshot.Fields) {
                        identity.AddRecord("identity-" + index++, field.Name, record => record.Cell("Artifact", snapshot.EvidenceIdentity).Cell("Section", field.Section)
                            .Cell("Name", field.Name).Cell("Value", field.Value).Cell("Line", field.Line.ToString()).Cell("Context", snapshot.ExecutionContext)
                            .Cell("Captured at", snapshot.CapturedAt?.ToString("O") ?? "Unknown"));
                    }
                }
                page.Panel("Identity snapshot", panel => panel.Content(identity));
            }
        });
        document.Body.Add(dashboard);
        return document.ToString();
    }

    private static MonitoringRecordExplorer Explorer(string label, params string[] columns) {
        var explorer = new MonitoringRecordExplorer().Settings(settings => settings.AccessibleLabel(label).PageSize(25)
            .InlineDetails(false).DrawerInitiallyCollapsed(true).OverviewDrawerLabel("Details and guidance").RawDrawerPanel(false).ExportDrawerPanel(false).End());
        for (int index = 0; index < columns.Length; index++) { explorer.AddColumn(columns[index], columns[index], visible: index < 3); }
        explorer.AddColumnGroup("Summary", columns.Take(3)).ActiveGroup("Summary");
        if (columns.Length > 3) { explorer.AddColumnGroup("All evidence", columns); }
        return explorer;
    }
}