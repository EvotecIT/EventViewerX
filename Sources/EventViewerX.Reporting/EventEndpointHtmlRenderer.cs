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
        dashboard.AddPage("investigation", "Findings and evidence", "Conclusions bounded by supplied evidence", TablerIconType.ReportAnalytics, page => {
            page.AddMetric(metric => metric.Title("Input parsing").Value(analysis.InputComplete ? "Complete" : "Incomplete")
                .State(analysis.InputComplete ? MonitoringHealthState.Healthy : MonitoringHealthState.Warning)
                .Description("Describes supplied bytes, not endpoint or lifecycle coverage"));
            page.AddMetric(metric => metric.Title("Application attempts").Value(analysis.Applications.Length.ToString()).Description("Explicitly attributable records"));
            var findingsExplorer = Explorer("Diagnostic findings", "Rule", "Title", "Status");
            var evidence = analysis.Records.ToDictionary(record => record.Identity, StringComparer.Ordinal);
            var artifacts = new HashSet<string>(analysis.IdentitySnapshots.Select(snapshot => snapshot.EvidenceIdentity), StringComparer.Ordinal);
            for (int index = 0; index < analysis.Findings.Length; index++) {
                EventDiagnosticFinding finding = analysis.Findings[index];
                findingsExplorer.AddRecord("finding-" + index, finding.Title, record => {
                    record.Cell("Rule", finding.RuleId).Cell("Title", finding.Title).Cell("Status", finding.Status).Detail("Explanation", finding.Explanation)
                        .Detail("Next checks", string.Join("\n", finding.NextChecks.Select(check => check.Artifact + ": " + check.Action + " Reason: " + check.Reason)))
                        .Detail("Rule version", finding.RuleVersion).Detail("References", string.Join("\n", finding.References)).Tag(finding.Status);
                    foreach (string id in finding.EvidenceIdentities) {
                        if (evidence.TryGetValue(id, out EventDiagnosticRecord? item)) {
                            record.DetailRecordLink("Log evidence", item.Source + ": lines " + item.LineStart + "-" + item.LineEnd + ", bytes " + item.ByteStart + "-" + item.ByteEnd,
                                "evidence-" + id, "Evidence");
                        } else if (artifacts.Contains(id)) {
                            record.DetailRecordLink("Identity evidence", "Captured DSRegCmd: " + id, "artifact-" + id, "Evidence");
                        } else {
                            record.Detail("Evidence artifact", "SHA-256: " + id, "Evidence");
                        }
                    }
                });
            }
            page.Panel("Findings", panel => panel.Subtitle("Select a finding, then its Evidence tab to open supporting coordinates").Content(findingsExplorer));
            var coverage = Explorer("Coverage limits", "Diagnostic");
            for (int index = 0; index < analysis.CoverageDiagnostics.Length; index++) {
                string diagnostic = analysis.CoverageDiagnostics[index];
                coverage.AddRecord("coverage-" + index, "Evidence limit", record => record.Cell("Diagnostic", diagnostic));
            }
            page.Panel("Coverage and uncertainty", panel => panel.Content(coverage));
            string[] logColumns = { "Source", "Lines", "Bytes", "Time", "Time quality", "Format", "Component", "Severity" };
            if (includeSensitiveEvidence) { logColumns = logColumns.Concat(new[] { "Message" }).ToArray(); }
            var explorer = Explorer("Captured log records", logColumns);
            foreach (EventDiagnosticRecord item in analysis.Records) {
                explorer.AddRecord("evidence-" + item.Identity, item.Source + ":" + item.LineStart, record => {
                    record.Cell("Source", item.Source).Cell("Lines", item.LineStart + "-" + item.LineEnd).Cell("Bytes", item.ByteStart + "-" + item.ByteEnd)
                        .Cell("Time", item.Timestamp?.ToString("O") ?? "Unknown instant").Cell("Time quality", item.TimeQuality).Cell("Format", item.Format)
                        .Cell("Component", item.Component).Cell("Severity", item.Severity?.ToString() ?? "Unknown").Detail("Identity", item.Identity)
                        .Detail("Diagnostic", item.Diagnostic ?? "None");
                    if (includeSensitiveEvidence) { record.Cell("Message", item.Message).Detail("Raw frame", item.RawText).Detail("Writer context", item.Context); }
                });
            }
            page.Panel("Log evidence", panel => panel.Subtitle(includeSensitiveEvidence ? "Sensitive captured records included" : "Coordinates only; raw evidence stays in the original session").Content(explorer));
            var summaries = Explorer("Captured identity artifacts", "Artifact", "Kind", "Captured at", "Context");
            foreach (var group in analysis.IdentitySnapshots.GroupBy(snapshot => snapshot.EvidenceIdentity, StringComparer.Ordinal)) {
                summaries.AddRecord("artifact-" + group.Key, "Captured DSRegCmd", record => record.Cell("Artifact", group.Key)
                    .Cell("Kind", "DSRegCmd status").Cell("Captured at", string.Join("; ", group.Select(snapshot => snapshot.CapturedAt?.ToString("O") ?? "Unknown").Distinct()))
                    .Cell("Context", string.Join(", ", group.Select(snapshot => snapshot.ExecutionContext).Distinct())).Detail("Identity", group.Key)
                    .Detail("Coverage", "Captured fields describe this snapshot and execution context; missing fields and later device state remain unknown."));
            }
            if (analysis.IdentitySnapshots.Length > 0) { page.Panel("Identity artifacts", panel => panel.Content(summaries)); }
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
        }, active: true);
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
