using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EventViewerX {
    /// <summary>
    /// Reconstructs PowerShell script blocks and execution records from the shared event engine.
    /// </summary>
    public static partial class PowerShellEventEngine {
        /// <summary>Default maximum number of incomplete script groups retained during reconstruction.</summary>
        public const int DefaultPowerShellScriptPendingLimit = 512;

        /// <summary>Default maximum number of event snapshots retained across incomplete script groups.</summary>
        public const int DefaultPowerShellScriptEventCacheLimit = 2048;

        /// <summary>Maximum accepted fragment number declared by one PowerShell script-block event.</summary>
        public const int MaximumPowerShellScriptPartCount = 4096;

        private static Dictionary<string, string?> ParseContextInfo(string? context) {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(context)) {
                return result;
            }
            var lines = context!.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines) {
                var parts = line.Split(new[] { '=' }, 2);
                var key = parts[0].Trim().Replace(" ", string.Empty);
                var value = parts.Length > 1 ? parts[1].Trim() : null;
                result[key] = value;
            }
            return result;
        }

        private static Dictionary<string, string?> GetAllData(EventObject eventObject) {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> data in eventObject.Data) {
                result[data.Key] = data.Value;
            }
            return result;
        }

        private static string FormatScript(string script) {
            var sb = new StringBuilder();
            int indent = 0;
            var lines = script.Replace("\r", string.Empty).Split('\n');
            foreach (var raw in lines) {
                var line = raw.Trim();
                if (line.StartsWith("}", StringComparison.Ordinal)) {
                    indent = Math.Max(0, indent - 4);
                }
                sb.Append(' ', indent);
                sb.AppendLine(line);
                if (line.EndsWith("{", StringComparison.Ordinal)) {
                    indent += 4;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Restores PowerShell scripts from operational logs for the specified edition.
        /// </summary>
        /// <param name="type">Windows PowerShell or PowerShell Core.</param>
        /// <param name="machineName">Remote machine to query; <c>null</c> targets local logs.</param>
        /// <param name="eventLogPath">Custom path to an .evtx file; <c>null</c> reads the live log.</param>
        /// <param name="dateFrom">Optional start time filter.</param>
        /// <param name="dateTo">Optional end time filter.</param>
        /// <param name="format">Whether to re-indent the recovered script text.</param>
        /// <param name="containsText">Optional text filters applied to the script content.</param>
        /// <param name="maxScripts">Maximum scripts to return. Zero returns every matching script.</param>
        /// <param name="maxEventsScanned">Maximum native records to scan. Zero scans the complete query.</param>
        /// <param name="maxPendingScripts">Maximum incomplete script groups retained while scanning.</param>
        /// <param name="maxCachedEvents">Maximum event snapshots retained across incomplete script groups.</param>
        /// <param name="cancellationToken">Cancellation token used to interrupt native reads.</param>
        /// <param name="executionInfo">Optional reusable completion record populated while the query runs.</param>
        /// <returns>
        /// With a positive <paramref name="maxScripts"/>, the newest matching script blocks in native encounter order.
        /// Unlimited queries stream complete blocks as they are reconstructed and emit bounded incomplete groups at the end.
        /// </returns>
        public static IEnumerable<RestoredPowerShellScript> GetPowerShellScripts(
            PowerShellEdition type,
            string? machineName = null,
            string? eventLogPath = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            bool format = false,
            IEnumerable<string>? containsText = null,
            int maxScripts = 0,
            int maxEventsScanned = 0,
            int maxPendingScripts = DefaultPowerShellScriptPendingLimit,
            int maxCachedEvents = DefaultPowerShellScriptEventCacheLimit,
            CancellationToken cancellationToken = default,
            PowerShellScriptQueryExecutionInfo? executionInfo = null) {
            return RestorePowerShellScripts(
                type,
                machineName,
                eventLogPath,
                dateFrom,
                dateTo,
                format,
                containsText,
                maxScripts,
                maxEventsScanned,
                maxPendingScripts,
                maxCachedEvents,
                cancellationToken,
                executionInfo);
        }
    }
}
