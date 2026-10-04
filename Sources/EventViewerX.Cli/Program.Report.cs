using System.Text;
using System.Text.Json;
using EventViewerX.Reporting;
using EventViewerX.Storage;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static async Task<int> ReportAsync(CliArguments options) {
        ValidateQuerySource(options, allowSummary: true);
        ValidateOccurrenceOptions(options);
        ValidateQuerySummaryPath(options);
        ValidateReportOutputPaths(options);
        using var cancellation = new ConsoleQueryCancellation();
        CancellationToken token = cancellation.Token;
        (EventReportPrivacyOptions? privacy, byte[]? key) = LoadReportPrivacy(options);
        try {
            EventReport report;
            string? queryJson = null;
            string? definitionJson = null;
            if (options.Get("store") is string storePath) {
                var store = new EventStore(storePath);
                EventStoreQuery query = CreateStoreQuery(options);
                report = options.Get("summary") is string summary
                    ? await store.CreateSummaryReportAsync(
                        query,
                        ParseSummaryPeriod(summary),
                        options.Get("title"), token).ConfigureAwait(false)
                    : await store.ReadReportAsync(query, options.Get("title"), token).ConfigureAwait(false);
                if (privacy == null && options.Has("bundle")) { queryJson = JsonSerializer.Serialize(new { Store = Path.GetFullPath(storePath), Query = query }, JsonOptions); }
            } else {
                if (options.Get("context-store") != null) {
                    report = await QueryGroupPolicyReportAsync(options, token).ConfigureAwait(false);
                } else {
                    EventReportRequest request = CreateRequest(options);
                    if (privacy == null && options.Has("bundle")) {
                        queryJson = JsonSerializer.Serialize(new { request.Types, request.LogName, request.Paths,
                            request.ReadMode, request.Predicate, request.EventIds, request.RecordIds, request.MachineNames,
                            request.Collectors, request.CollectorLogName, request.StartTime, request.EndTime,
                            request.TimePeriod, request.MaxEvents, request.MaxCandidates, request.Oldest }, JsonOptions);
                        definitionJson = request.Definition == null ? null : JsonSerializer.Serialize(request.Definition, JsonOptions);
                    }
                    report = await EventReportEngine.QueryAsync(request, token).ConfigureAwait(false);
                }
                await WriteStoreIfRequestedAsync(report, options, token).ConfigureAwait(false);
            }
            report = ApplyOccurrenceGrouping(report, options);
            if (privacy != null) { report = EventReportPrivacy.Apply(report, privacy, key, token); }
            token.ThrowIfCancellationRequested();
            bool written = false;
            EventEmailPackage? emailPackage = null;
            if (options.Get("html") is string html) {
                var htmlOptions = new EventReportHtmlOptions {
                    RecordDrawerPlacement = ParseDrawerPlacement(options.Get("drawer-placement"))
                };
                Console.WriteLine(EventReportHtmlRenderer.Save(report, html, htmlOptions));
                written = true;
            }
            if (options.Get("excel") is string excel) {
                Console.WriteLine(EventReportExcelRenderer.Save(report, excel));
                written = true;
            }
            if (options.Get("csv") is string csv) {
                Console.WriteLine(EventReportCsvRenderer.Save(report, csv));
                written = true;
            }
            if (options.Get("email-html") is string emailHtml) {
                emailPackage = await EventReportEmailRenderer.RenderAsync(report, options.GetInt("email-rows", 25)).ConfigureAwait(false);
                string fullPath = Path.GetFullPath(emailHtml);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, emailPackage.Html, new UTF8Encoding(false)).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.ChangeExtension(fullPath, ".txt"), emailPackage.PlainText, new UTF8Encoding(false)).ConfigureAwait(false);
                Console.WriteLine(fullPath);
                written = true;
            }
            if (options.Get("mail-profile") is string mailProfile) {
                emailPackage ??= await EventReportEmailRenderer.RenderAsync(report, options.GetInt("email-rows", 25)).ConfigureAwait(false);
                SmtpNotificationProfile profile = SmtpNotificationProfile.Load(mailProfile);
                Mailozaurr.SmtpResult result = await profile.SendAsync(emailPackage, report.Title).ConfigureAwait(false);
                Console.WriteLine(JsonSerializer.Serialize(new {
                    Delivered = result.Status,
                    profile.DryRun,
                    result.Server,
                    result.Port,
                    result.MessageId,
                    result.TimeToExecute
                }, JsonOptions));
                written = true;
            }
            if (options.Get("bundle") is string bundle) {
                SaveEvidenceBundle(report, bundle, options, queryJson, definitionJson, privacy, token);
                Console.WriteLine(Path.GetFullPath(bundle));
                written = true;
            }
            if (!written) {
                throw new ArgumentException("report requires --html, --excel, --csv, --email-html, --bundle, or --mail-profile.");
            }
            return CompleteQuery(EventReportSummary.Create(report), options, token);
        } finally {
            if (key != null) { System.Security.Cryptography.CryptographicOperations.ZeroMemory(key); }
        }
    }

}
