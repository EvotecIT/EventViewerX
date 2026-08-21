using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace EventViewerX {
    public static partial class PowerShellEventEngine {
        /// <summary>
        /// Retrieves execution records (event 4100) for PowerShell script blocks.
        /// </summary>
        /// <param name="type">Windows PowerShell or PowerShell Core.</param>
        /// <param name="machineName">Remote machine to query; <c>null</c> targets local logs.</param>
        /// <param name="eventLogPath">Optional .evtx file path to read from instead of the live log.</param>
        /// <param name="dateFrom">Lower bound for the query time window.</param>
        /// <param name="dateTo">Upper bound for the query time window.</param>
        /// <param name="maxEvents">Maximum records to return. Zero returns every matching record.</param>
        /// <param name="maxEventsScanned">Maximum native event records to scan. Zero scans the complete query.</param>
        /// <param name="cancellationToken">Cancellation token used to interrupt native reads.</param>
        /// <param name="executionInfo">Optional reusable completion record populated while the query runs.</param>
        /// <returns>Execution records in reverse chronological order.</returns>
        public static IEnumerable<PowerShellScriptExecutionInfo> GetPowerShellScriptExecution(
            PowerShellEdition type,
            string? machineName = null,
            string? eventLogPath = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int maxEvents = 0,
            int maxEventsScanned = 0,
            CancellationToken cancellationToken = default,
            PowerShellScriptQueryExecutionInfo? executionInfo = null) {

            ValidatePowerShellScriptLimits(maxEvents, nameof(maxEvents), maxEventsScanned);
            cancellationToken.ThrowIfCancellationRequested();
            PowerShellScriptQueryExecutionInfo queryInfo = executionInfo ?? new PowerShellScriptQueryExecutionInfo();
            queryInfo.Reset(machineName, eventLogPath, maxEvents, maxEventsScanned);
            string logName =
                GetPowerShellLogName(type);

            return GetPowerShellScriptExecutionIterator(
                logName,
                machineName,
                eventLogPath,
                dateFrom,
                dateTo,
                maxEvents,
                maxEventsScanned,
                cancellationToken,
                queryInfo);
        }

        private static IEnumerable<PowerShellScriptExecutionInfo>
            GetPowerShellScriptExecutionIterator(
                string logName,
                string? machineName,
                string? eventLogPath,
                DateTime? dateFrom,
                DateTime? dateTo,
                int maxEvents,
                int maxEventsScanned,
                CancellationToken cancellationToken,
                PowerShellScriptQueryExecutionInfo queryInfo) {

            var scanLimit = new PowerShellScriptScanLimit(
                maxEventsScanned);
            int returned = 0;
            foreach (EventObject eventObject in QueryPowerShellScriptEvents(
                         logName,
                         new[] { "4100" },
                         machineName,
                         eventLogPath,
                         dateFrom,
                         dateTo,
                         scanLimit.NativeReadLimit,
                         cancellationToken)) {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!scanLimit.TryAcceptCandidate()) {
                        break;
                    }
                    queryInfo.EventsScanned =
                        scanLimit.EventsScanned;
                    if (!TryRecordPowerShellScriptExecutionResult(
                            maxEvents,
                            ref returned,
                            queryInfo)) {
                        break;
                    }

                    string? contextInfo = eventObject.GetDataValueOrEmpty("ContextInfo");
                    var data = ParseContextInfo(contextInfo);
                    yield return new PowerShellScriptExecutionInfo(eventObject, data);
            }
            queryInfo.ScanLimitReached =
                scanLimit.LimitReached;
        }

        internal static bool
            TryRecordPowerShellScriptExecutionResult(
                int maxEvents,
                ref int returned,
                PowerShellScriptQueryExecutionInfo queryInfo) {

            if (maxEvents > 0 &&
                returned >= maxEvents) {
                queryInfo.OutputLimitReached = true;
                return false;
            }
            returned++;
            queryInfo.RecordResult();
            return true;
        }

        /// <summary>
        /// Reassembles PowerShell script blocks (events 4103/4104) from the operational log.
        /// </summary>
        /// <param name="type">Windows PowerShell or PowerShell Core.</param>
        /// <param name="machineName">Remote machine to query; <c>null</c> targets local logs.</param>
        /// <param name="eventLogPath">Optional .evtx file path to read from instead of the live log.</param>
        /// <param name="dateFrom">Lower bound for the query time window.</param>
        /// <param name="dateTo">Upper bound for the query time window.</param>
        /// <param name="format">When true, re-indents the captured script text.</param>
        /// <param name="containsText">Optional text filters applied to reconstructed scripts.</param>
        /// <param name="maxScripts">Maximum reconstructed scripts to return. Zero returns every matching script.</param>
        /// <param name="maxEventsScanned">Maximum native records to scan. Zero scans the complete query.</param>
        /// <param name="maxPendingScripts">Maximum incomplete script groups retained while scanning.</param>
        /// <param name="maxCachedEvents">Maximum event snapshots retained across incomplete script groups.</param>
        /// <param name="cancellationToken">Cancellation token used to interrupt native reads.</param>
        /// <param name="executionInfo">Optional reusable completion record populated while the query runs.</param>
        /// <returns>
        /// With a positive <paramref name="maxScripts"/>, the newest matching script blocks in native encounter order.
        /// Unlimited queries stream complete blocks as they are reconstructed and emit bounded incomplete groups at the end.
        /// </returns>
        public static IEnumerable<RestoredPowerShellScript> RestorePowerShellScripts(
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

            ValidatePowerShellScriptLimits(maxScripts, nameof(maxScripts), maxEventsScanned);
            cancellationToken.ThrowIfCancellationRequested();
            if (maxPendingScripts <= 0) {
                throw new ArgumentOutOfRangeException(nameof(maxPendingScripts), "Maximum pending scripts must be positive.");
            }
            if (maxCachedEvents <= 0) {
                throw new ArgumentOutOfRangeException(nameof(maxCachedEvents), "Maximum cached events must be positive.");
            }
            bool ownsQueryInfo = executionInfo == null;
            PowerShellScriptQueryExecutionInfo queryInfo = executionInfo ?? new PowerShellScriptQueryExecutionInfo();
            queryInfo.Reset(machineName, eventLogPath, maxScripts, maxEventsScanned, maxPendingScripts, maxCachedEvents);

            string[] textFilters = containsText?
                .Where(static term => term != null)
                .ToArray() ?? Array.Empty<string>();
            string logName =
                GetPowerShellLogName(type);

            return RestorePowerShellScriptsIterator(
                logName,
                machineName,
                eventLogPath,
                dateFrom,
                dateTo,
                format,
                textFilters,
                maxScripts,
                maxEventsScanned,
                maxPendingScripts,
                maxCachedEvents,
                cancellationToken,
                queryInfo,
                ownsQueryInfo);
        }

        private static IEnumerable<RestoredPowerShellScript>
            RestorePowerShellScriptsIterator(
                string logName,
                string? machineName,
                string? eventLogPath,
                DateTime? dateFrom,
                DateTime? dateTo,
                bool format,
                IReadOnlyList<string> textFilters,
                int maxScripts,
                int maxEventsScanned,
                int maxPendingScripts,
                int maxCachedEvents,
                CancellationToken cancellationToken,
                PowerShellScriptQueryExecutionInfo queryInfo,
                bool ownsQueryInfo) {

            var cache = new PowerShellScriptFragmentCache(maxPendingScripts, maxCachedEvents);
            List<KeyValuePair<long, RestoredPowerShellScript>>? boundedScripts = maxScripts > 0
                ? new List<KeyValuePair<long, RestoredPowerShellScript>>(Math.Min(maxScripts, 256))
                : null;
            try {
                var scanLimit =
                    new PowerShellScriptScanLimit(
                        maxEventsScanned);
                int returned = 0;
                foreach (EventObject eventObject in QueryPowerShellScriptEvents(
                             logName,
                             new[] { "4103", "4104" },
                             machineName,
                             eventLogPath,
                             dateFrom,
                             dateTo,
                             scanLimit.NativeReadLimit,
                             cancellationToken)) {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!scanLimit.TryAcceptCandidate()) {
                        break;
                    }
                    queryInfo.EventsScanned =
                        scanLimit.EventsScanned;

                    string? scriptText = eventObject.GetDataValueOrEmpty("ScriptBlockText");
                    if (string.IsNullOrEmpty(scriptText) || scriptText == "0") {
                        continue;
                    }
                    string nonNullScriptText = scriptText!;
                    string scriptId = eventObject.GetDataValueOrEmpty("ScriptBlockId");
                    if (string.IsNullOrEmpty(scriptId)) {
                        continue;
                    }
                    int messageNumber = ParseBoundedFragmentNumber(eventObject.GetDataValueOrEmpty("MessageNumber"), out bool invalidMessageNumber);
                    int messageTotal = ParseBoundedFragmentNumber(eventObject.GetDataValueOrEmpty("MessageTotal"), out bool invalidMessageTotal);
                    if (invalidMessageNumber || invalidMessageTotal) {
                        queryInfo.InvalidFragmentMetadataEvents++;
                    }
                    if (invalidMessageNumber) {
                        continue;
                    }
                    if (cache.TryAdd(scriptId, messageNumber, messageTotal, nonNullScriptText, eventObject, out PowerShellScriptAssembly? completed) &&
                        completed != null &&
                        TryBuildRestoredPowerShellScript(completed, format, textFilters, out RestoredPowerShellScript restored)) {
                        if (boundedScripts != null) {
                            queryInfo.TryRecordMatchingResult();
                            AddBoundedRestoredPowerShellScript(
                                boundedScripts,
                                completed.EncounterOrder,
                                restored,
                                maxScripts);
                        } else {
                            if (!restored.IsComplete) {
                                queryInfo.IncompleteScriptsReturned++;
                            }
                            returned++;
                            queryInfo.ResultsReturned = returned;
                            yield return restored;
                        }
                    }

                    if (boundedScripts != null &&
                        queryInfo.OutputLimitReached &&
                        CanFinalizeBoundedPowerShellScriptSelection(
                            boundedScripts,
                            maxScripts,
                            cache.NewestPendingEncounterOrder)) {
                        break;
                    }
                }
                queryInfo.ScanLimitReached =
                    scanLimit.LimitReached;
                if (queryInfo.ScanLimitReached) {
                    Settings._logger.WriteVerbose($"PowerShellScripts: stopped after reaching the {maxEventsScanned} event scan limit.");
                }

                foreach (PowerShellScriptAssembly pending in cache.Drain()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!TryBuildRestoredPowerShellScript(pending, format, textFilters, out RestoredPowerShellScript restored)) {
                        continue;
                    }

                    if (boundedScripts != null) {
                        queryInfo.TryRecordMatchingResult();
                        AddBoundedRestoredPowerShellScript(
                            boundedScripts,
                            pending.EncounterOrder,
                            restored,
                            maxScripts);
                        continue;
                    }

                    if (!restored.IsComplete) {
                        queryInfo.IncompleteScriptsReturned++;
                    }
                    returned++;
                    queryInfo.ResultsReturned = returned;
                    yield return restored;
                }

                if (boundedScripts != null) {
                    foreach (KeyValuePair<long, RestoredPowerShellScript> selected in boundedScripts.OrderBy(static item => item.Key)) {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!selected.Value.IsComplete) {
                            queryInfo.IncompleteScriptsReturned++;
                        }
                        returned++;
                        queryInfo.ResultsReturned = returned;
                        yield return selected.Value;
                    }
                }
            }
            finally {
                queryInfo.EvictedIncompleteScripts = cache.EvictedScriptCount;
                queryInfo.EvictedCachedEvents = cache.EvictedEventCount;
                if (ownsQueryInfo && cache.EvictedScriptCount > 0) {
                    Settings._logger.WriteWarning(
                        $"PowerShellScripts: evicted {cache.EvictedScriptCount} incomplete script groups " +
                        $"containing {cache.EvictedEventCount} events after reaching the configured cache bounds.");
                }
            }
        }

        internal static bool CanFinalizeBoundedPowerShellScriptSelection(
            IReadOnlyList<KeyValuePair<long, RestoredPowerShellScript>> selected,
            int maxScripts,
            long? newestPendingEncounterOrder) {

            if (selected == null) {
                throw new ArgumentNullException(nameof(selected));
            }
            if (maxScripts <= 0) {
                throw new ArgumentOutOfRangeException(nameof(maxScripts), "Maximum scripts must be positive.");
            }
            if (selected.Count < maxScripts) {
                return false;
            }

            long oldestSelectedOrder = selected.Max(static item => item.Key);
            return !newestPendingEncounterOrder.HasValue ||
                   newestPendingEncounterOrder.Value > oldestSelectedOrder;
        }

        internal static void AddBoundedRestoredPowerShellScript(
            List<KeyValuePair<long, RestoredPowerShellScript>> selected,
            long encounterOrder,
            RestoredPowerShellScript script,
            int maxScripts) {

            if (selected == null) {
                throw new ArgumentNullException(nameof(selected));
            }
            if (script == null) {
                throw new ArgumentNullException(nameof(script));
            }
            if (maxScripts <= 0) {
                throw new ArgumentOutOfRangeException(nameof(maxScripts), "Maximum scripts must be positive.");
            }

            var candidate = new KeyValuePair<long, RestoredPowerShellScript>(encounterOrder, script);
            if (selected.Count < maxScripts) {
                selected.Add(candidate);
                return;
            }

            int oldestIndex = 0;
            for (int index = 1; index < selected.Count; index++) {
                if (selected[index].Key > selected[oldestIndex].Key) {
                    oldestIndex = index;
                }
            }
            if (encounterOrder < selected[oldestIndex].Key) {
                selected[oldestIndex] = candidate;
            }
        }

        private static void ValidatePowerShellScriptLimits(int maxOutput, string maxOutputParameterName, int maxEventsScanned) {
            if (maxOutput < 0) {
                throw new ArgumentOutOfRangeException(maxOutputParameterName, "Maximum output events must be greater than or equal to zero.");
            }
            if (maxEventsScanned < 0) {
                throw new ArgumentOutOfRangeException(nameof(maxEventsScanned), "Maximum scanned events must be greater than or equal to zero.");
            }
        }

        private static string GetPowerShellLogName(
            PowerShellEdition type) {

            return type switch {
                PowerShellEdition.WindowsPowerShell =>
                    "Microsoft-Windows-PowerShell/Operational",
                PowerShellEdition.PowerShell =>
                    "PowerShellCore/Operational",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(type),
                    type,
                    "The PowerShell edition is not supported.")
            };
        }

        private static int ParseBoundedFragmentNumber(string? value, out bool invalid) {
            invalid = false;
            if (string.IsNullOrWhiteSpace(value)) {
                return 0;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ||
                parsed < 0 ||
                parsed > MaximumPowerShellScriptPartCount) {
                invalid = true;
                return 0;
            }

            return parsed;
        }

        private static bool TryBuildRestoredPowerShellScript(
            PowerShellScriptAssembly assembly,
            bool format,
            IReadOnlyList<string> containsText,
            out RestoredPowerShellScript restored) {

            var scriptBuilder = new StringBuilder();
            foreach (KeyValuePair<int, string> part in assembly.Parts.OrderBy(static pair => pair.Key)) {
                scriptBuilder.Append(part.Value);
            }

            string script = scriptBuilder.ToString();
            for (int index = 0; index < containsText.Count; index++) {
                if (script.IndexOf(containsText[index], StringComparison.OrdinalIgnoreCase) < 0) {
                    restored = null!;
                    return false;
                }
            }

            if (format) {
                script = FormatScript(script);
            }

            EventObject metaRecord = assembly.MetaRecord ?? assembly.Events[0];
            restored = new RestoredPowerShellScript {
                ScriptBlockId = assembly.ScriptBlockId,
                Script = script,
                IsComplete = assembly.IsComplete,
                ExpectedPartCount = assembly.ExpectedParts,
                AvailablePartCount = assembly.Parts.Count,
                Events = assembly.Events,
                Data = GetAllData(metaRecord)
            };
            return true;
        }
    }
}
