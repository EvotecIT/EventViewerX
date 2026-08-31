using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace PSEventViewer;

public sealed partial class CmdletGetEVXEvent {
    /// <summary>
    /// Initializes logging and loads persisted checkpoint state.
    /// </summary>
    protected override Task BeginProcessingAsync() {
        _eventsOutput = 0;
        if (PortableEvtx.IsPresent && !string.IsNullOrWhiteSpace(PortableEvtxExecutable)) {
            throw new PSArgumentException(
                "PortableEvtx and PortableEvtxExecutable are mutually exclusive. Select one portable EVTX engine.");
        }
        if (PortableEvtx.IsPresent || !string.IsNullOrWhiteSpace(PortableEvtxExecutable)) {
            if (Path.Length == 0) {
                throw new PSArgumentException(
                    "Portable EVTX parsing requires at least one -Path source.",
                    nameof(Path));
            }
            _resolvedSavedEventReader = string.IsNullOrWhiteSpace(PortableEvtxExecutable)
                ? _portableSavedEventReader
                : new EvtxDumpSavedEventReader(PortableEvtxExecutable!);
        }
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        var internalLogger = new InternalLogger(false);
        var internalLoggerPowerShell = new InternalLoggerPowerShell(internalLogger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        SetEventViewerLogger(internalLogger);
        if (!string.IsNullOrWhiteSpace(RecordIdFile)) {
            EventCheckpointSnapshot checkpointSnapshot = EventCheckpointStore.Load(RecordIdFile!);
            _recordMap = checkpointSnapshot.Records.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value,
                StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, EventCheckpointValue> checkpoint in checkpointSnapshot.Checkpoints) {
                _checkpointGenerations[checkpoint.Key] = checkpoint.Value.GenerationId;
                if (!string.IsNullOrWhiteSpace(checkpoint.Value.BoundaryIdentity)) {
                    _checkpointBoundaries[checkpoint.Key] = checkpoint.Value.BoundaryIdentity!;
                }
            }
        }
        return Task.CompletedTask;
    }

    private void InitializeCheckpointKey(EventPredicate? typedPredicate) {
        if (!UsesCheckpoint) {
            _recordIdKey = string.Empty;
            return;
        }
        _recordIdKey = !string.IsNullOrEmpty(RecordIdKey)
            ? RecordIdKey!
            : BuildDefaultCheckpointKey(typedPredicate);
        if (!string.IsNullOrEmpty(RecordIdKey) || _typedFilter != null ||
            typedPredicate != null && (UsesBuiltInTypeQuery || UsesCustomDefinitionQuery)) {
            return;
        }
        string legacyKey = BuildLegacyCheckpointKey();
        if (!_checkpointGenerations.ContainsKey(_recordIdKey) &&
            !_recordMap.ContainsKey(_recordIdKey) &&
            _recordMap.TryGetValue(
                legacyKey,
                out long legacyRecordId)) {
            _recordMap[_recordIdKey] = legacyRecordId;
            if (_checkpointBoundaries.TryGetValue(
                    legacyKey,
                    out string? legacyBoundary)) {
                _checkpointBoundaries[_recordIdKey] =
                    legacyBoundary;
            }
        }
    }

    private string BuildDefaultCheckpointKey(EventPredicate? typedPredicate) {
        IReadOnlyList<string?> checkpointMachines = GetEffectiveCheckpointMachines();
        string sourceIdentity = ParameterSetName switch {
            "Type" => "Named:" +
                             string.Join(",", Type.OrderBy(static value => value)) +
                             "|Log:" +
                             string.Join(",", LogName
                                 .Select(static log => log.Trim().ToUpperInvariant())
                                 .OrderBy(static log => log, StringComparer.OrdinalIgnoreCase)),
            "Definition" => "Definition:" + JsonSerializer.Serialize(ResolveEventDefinition()) +
                            "|Path:" + string.Join(",", Path
                                .Select(NormalizeCheckpointPath)
                                .OrderBy(static path => path, CheckpointPathComparer)),
            "Path" => "Path:" + string.Join(",", Path
                .Select(NormalizeCheckpointPath)
                .OrderBy(static path => path, CheckpointPathComparer)),
            "Hashtable" => "Hashtable",
            "Xml" =>
                "Xml:" + FilterXml?.OuterXml,
            "Provider" => "Provider:" + string.Join(
                ",",
                (ProviderName ?? Array.Empty<string>())
                    .Select(static provider => provider.Trim().ToUpperInvariant())
                    .OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase)),
            _ => "Log:" + string.Join(",", LogName
                .Select(static log => log.Trim().ToUpperInvariant())
                .OrderBy(static log => log, StringComparer.OrdinalIgnoreCase))
        };

        var identity = new List<string> {
            ParameterSetName,
            sourceIdentity,
            "Machines",
            string.Join(",", checkpointMachines
                .Select(static machine => string.IsNullOrWhiteSpace(machine) ? "<LOCAL>" : machine!.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static machine => machine, StringComparer.OrdinalIgnoreCase)),
            "EventIds",
            string.Join(",", (EventId ?? Array.Empty<int>()).Distinct().OrderBy(static value => value)),
            "RecordIds",
            string.Join(",", (EventRecordId ?? Array.Empty<long>()).Distinct().OrderBy(static value => value)),
            "Provider",
            string.Join(",", (ProviderName ?? Array.Empty<string>())
                .Select(static value => value.Trim().ToUpperInvariant())
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)),
            "Keywords",
            string.Join(",", (Keywords ?? Array.Empty<long>())
                .Distinct()
                .OrderBy(static value => value)),
            "Level",
            string.Join(",", (Level ?? Array.Empty<EventViewerX.Level>())
                .Select(static value => (int)value)
                .Distinct()
                .OrderBy(static value => value)),
            "StartTimeUtc",
            StartTime?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            "EndTimeUtc",
            EndTime?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            "TimePeriod",
            TimePeriod?.ToString() ?? string.Empty,
            "UserId",
            string.Join(",", (UserId ?? Array.Empty<string>())
                .Select(static value => value.Trim().ToUpperInvariant())
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)),
            "MessageRegex",
            MessageRegex?.ToString() ?? string.Empty,
            "MessageRegexOptions",
            MessageRegex == null ? string.Empty : ((int)MessageRegex.Options).ToString(CultureInfo.InvariantCulture),
            "MessageRegexCulture",
            MessageRegex == null ? string.Empty : CultureInfo.CurrentCulture.Name,
            "MessageCulture",
            MessageCulture?.Name ?? string.Empty,
            "FallbackMessageCulture",
            FallbackMessageCulture?.Name ?? string.Empty,
            "Oldest",
            EffectiveOldest.ToString(CultureInfo.InvariantCulture)
        };
        AddHashtableIdentity(identity, "NamedDataFilter", NamedDataFilter);
        AddHashtableIdentity(identity, "NamedDataExcludeFilter", NamedDataExcludeFilter);
        AddHashtableIdentity(identity, "FilterHashtable", FilterHashtable);
        if (_typedFilter != null) {
            AddTypedFilterIdentity(identity, _typedFilter);
        } else if (UsesBuiltInTypeQuery || UsesCustomDefinitionQuery) {
            AddTypedPredicateIdentity(identity, typedPredicate);
        } else {
            AddFilterIdentity(identity, ResolveNativeFilter());
        }

        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(identity)));
        string fingerprint = BitConverter.ToString(hash).Replace("-", string.Empty);
        return $"{sourceIdentity}|q:{fingerprint}";
    }

    private static StringComparer CheckpointPathComparer =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static string NormalizeCheckpointPath(string path) {
        string fullPath = System.IO.Path.GetFullPath(path)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? fullPath.ToUpperInvariant()
            : fullPath;
    }

    private static void AddHashtableIdentity(
        List<string> identity,
        string name,
        object? table) {
        identity.Add(name);
        AddCheckpointIdentityValue(identity, table);
    }

    private static void AddFilterIdentity(
        List<string> identity,
        EventFilter? filter) {

        identity.Add("TypedFilter");
        if (filter == null) {
            identity.Add("null");
            return;
        }
        AddCheckpointIdentityValue(identity, filter.EventIds);
        AddCheckpointIdentityValue(identity, filter.RecordIds);
        AddCheckpointIdentityValue(identity, filter.MinimumRecordIdExclusive);
        AddCheckpointIdentityValue(identity, filter.MaximumRecordIdExclusive);
        AddCheckpointIdentityValue(identity, filter.ProviderNames);
        AddCheckpointIdentityValue(identity, filter.Levels);
        AddCheckpointIdentityValue(identity, filter.Keywords);
        AddCheckpointIdentityValue(identity, filter.StartTime);
        AddCheckpointIdentityValue(identity, filter.EndTime);
        AddCheckpointIdentityValue(identity, filter.UserIds);
        AddCheckpointIdentityValue(identity, filter.Data);
        AddCheckpointIdentityValue(identity, filter.NamedData);
        AddCheckpointIdentityValue(identity, filter.ExcludedNamedData);
        AddCheckpointIdentityValue(identity, filter.ExcludedEventIds);
    }

    private static void AddTypedFilterIdentity(
        List<string> identity,
        PowerShellEventPredicateBuilder filter) {

        identity.Add("TypedPredicateFilter");
        identity.Add(filter.Type.HasValue
            ? "Type:" + filter.Type.Value
            : "Definition:" + JsonSerializer.Serialize(filter.Definition));
        identity.Add(filter.Predicate == null
            ? "Predicate:null"
            : "Predicate:" + filter.Predicate.ToJson(indented: false));
    }

    private static void AddTypedPredicateIdentity(
        List<string> identity,
        EventPredicate? predicate) {

        identity.Add("InlineTypedPredicate");
        identity.Add(predicate == null
            ? "Predicate:null"
            : "Predicate:" + predicate.ToJson(indented: false));
    }

    private static void AddCheckpointIdentityValue(
        List<string> identity,
        object? value) {

        if (value is PSObject psObject) {
            value = psObject.BaseObject;
        }
        if (value == null) {
            identity.Add("null");
            return;
        }
        if (value is IDictionary dictionary) {
            identity.Add("map:{");
            var entries = dictionary
                .Cast<DictionaryEntry>()
                .Select(entry => new {
                    Key = ConvertNamedDataValue(entry.Key),
                    entry.Value
                })
                .OrderBy(
                    static entry => entry.Key,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var entry in entries) {
                identity.Add("key:" + entry.Key.ToUpperInvariant());
                AddCheckpointIdentityValue(identity, entry.Value);
            }
            identity.Add("}");
            return;
        }
        if (value is IEnumerable enumerable &&
            value is not string) {
            var items = new List<string>();
            foreach (object? item in enumerable) {
                var itemIdentity = new List<string>();
                AddCheckpointIdentityValue(itemIdentity, item);
                items.Add(JsonSerializer.Serialize(itemIdentity));
            }
            identity.Add("list:[");
            identity.AddRange(items.OrderBy(
                static item => item,
                StringComparer.Ordinal));
            identity.Add("]");
            return;
        }
        if (value is DateTime) {
            identity.Add(
                "datetime:" +
                EventFilterValueConverter.ToInvariantString(value));
            return;
        }
        if (value is DateTimeOffset) {
            identity.Add(
                "datetimeoffset:" +
                EventFilterValueConverter.ToInvariantString(value));
            return;
        }
        string text = EventFilterValueConverter.ToInvariantString(value);
        identity.Add(
            value.GetType().FullName + ":" + text);
    }

    private string BuildLegacyCheckpointKey() {
        string queryIdentity = LogName.Length > 0
            ? string.Join(",", LogName)
            : Path.Length > 0
                ? string.Join(",", Path)
                : ParameterSetName == "Definition"
                    ? ResolveEventDefinition().Name
                    : "unknown";
        string machines = string.Join(",", Collector ?? MachineName ?? new List<string?>());
        return $"{queryIdentity}|{machines}";
    }

    /// <summary>
    /// Executes the event query based on provided parameters.
    /// </summary>
    private void ValidateRecordOptions() {
        if ((UsesBuiltInTypeQuery || UsesCustomDefinitionQuery) &&
            Collector != null &&
            MachineName != null) {
            throw new PSArgumentException(
                "-Collector and -MachineName cannot be used together. Use -Collector for ForwardedEvents or -MachineName for direct source queries.");
        }
        if (ExpandData && ReadMode != EventReadMode.StructuredData &&
            ReadMode != EventReadMode.Full &&
            ReadMode != EventReadMode.StructuredDataAndMessage) {
            throw new PSArgumentException("-ExpandData requires -ReadMode StructuredData, StructuredDataAndMessage, or Full.");
        }
        if (MessageRegex != null && ReadMode != EventReadMode.Message &&
            ReadMode != EventReadMode.Full &&
            ReadMode != EventReadMode.StructuredDataAndMessage) {
            throw new PSArgumentException("-MessageRegex requires -ReadMode Message, StructuredDataAndMessage, or Full.");
        }
    }

    private void PrepareRecordProcessing(CancellationToken token) {
        PrepareCheckpointBounds(token);
    }

    private bool TrackCheckpointProgress(EventObject eventObject) {
        if (!eventObject.RecordId.HasValue) {
            return true;
        }

        string checkpointKey = GetCheckpointKey(eventObject);
        long recordId = eventObject.RecordId.Value;
        bool hasCheckpoint = _recordMap.TryGetValue(checkpointKey, out long previousRecordId);
        if (hasCheckpoint && recordId <= previousRecordId) {
            return false;
        }
        if (!_highestRecordIds.TryGetValue(checkpointKey, out long highestRecordId) || recordId > highestRecordId) {
            _highestRecordIds[checkpointKey] = recordId;
            _highestCheckpointEvents[checkpointKey] = eventObject;
        }
        return true;
    }

    private string GetCheckpointKey(EventObject eventObject) {
        if (!UsesDerivedCheckpointKeys()) {
            return _recordIdKey;
        }

        string source = string.IsNullOrWhiteSpace(eventObject.QueriedMachine)
            ? eventObject.MachineName
            : eventObject.QueriedMachine;
        return $"{_recordIdKey}|{source}|{eventObject.ContainerLog}";
    }

    private long? GetCheckpointLowerBound(
        string? machineName,
        string logName) {

        return GetCheckpointLowerBound(
            machineName,
            logName,
            sourceIsFile: false);
    }

    private long? GetCheckpointLowerBound(
        string? machineName,
        string logName,
        bool sourceIsFile) {

        if (string.IsNullOrWhiteSpace(RecordIdFile)) {
            return null;
        }

        string? sourceIdentity =
            sourceIsFile
                ? logName
                : machineName;
        return TryGetCheckpoint(
                sourceIdentity,
                logName,
                out _,
                out long checkpoint)
            ? checkpoint
            : null;
    }

    private bool TryGetCheckpoint(string? machineName, string logName, out string checkpointKey, out long checkpoint) {
        checkpointKey = _recordIdKey;
        checkpoint = 0;

        if (!UsesDerivedCheckpointKeys()) {
            return _recordMap.TryGetValue(_recordIdKey, out checkpoint);
        }

        HashSet<string> sourceNames = GetCheckpointSourceNames(machineName);
        foreach (string sourceName in sourceNames) {
            string sourceKey = $"{_recordIdKey}|{sourceName}|{logName}";
            if (_recordMap.TryGetValue(sourceKey, out checkpoint)) {
                checkpointKey = sourceKey;
                return true;
            }
        }

        return false;
    }

    private bool UsesDerivedCheckpointKeys() {
        return ParameterSetName == "Type" ||
               ParameterSetName == "Definition" ||
               ParameterSetName == "TypedFilter" ||
               GetCheckpointSourceCount() > 1 ||
               GetEffectiveCheckpointMachines().Count > 1 ||
               (ParameterSetName == "Channel" &&
                MyInvocation.ExpectingInput);
    }

    private static HashSet<string> GetCheckpointSourceNames(string? machineName) {
        var sourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(machineName)) {
            sourceNames.Add(machineName!.Trim());
        } else {
            sourceNames.Add(Environment.MachineName);
            sourceNames.Add(
                EventLogTarget.LocalMachineName);
        }
        return sourceNames;
    }

    private IReadOnlyList<string?> GetEffectiveCheckpointMachines()
        => EventLogTarget.NormalizeMachineNames(Collector ?? MachineName);

    private void PrepareCheckpointBounds(CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(RecordIdFile) || _recordMap.Count == 0) {
            return;
        }

        IReadOnlyList<CheckpointSource> checkpointSources =
            GetCheckpointSources();
        foreach (CheckpointSource source in checkpointSources
                     .Where(static item => item.IsFile)) {
            string path = source.Name;
            if (!TryGetCheckpoint(
                    path,
                    path,
                    out string checkpointKey,
                    out long checkpoint)) {
                continue;
            }
            EventObject? boundaryEvent = checkpoint > 0
                ? EventLogEngine.ReadFile(
                    new EventLogFileQuery(path) {
                        XPath = EventFilterCompiler.BuildXPath(
                            new EventFilter {
                                RecordIds = new[] {
                                    checkpoint
                                }
                            }),
                        MaxEvents = 1,
                        ReadMode =
                            EventReadMode.Metadata
                    },
                    cancellationToken).FirstOrDefault()
                : null;
            EvaluateCheckpointBoundary(
                checkpointKey,
                checkpoint,
                boundaryEvent,
                path);
        }

        IReadOnlyList<string?> machines = GetEffectiveCheckpointMachines();

        foreach (CheckpointSource source in checkpointSources
                     .Where(static item => !item.IsFile)) {
            string log = source.Name;
            foreach (string? machine in machines) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetCheckpoint(machine, log, out string checkpointKey, out long checkpoint)) {
                    continue;
                }

                try {
                    EventObject? boundaryEvent = checkpoint > 0
                        ? EventLogEngine.ReadChannel(
                            new EventLogChannelQuery(log) {
                                MachineName = machine,
                                Credential =
                                    Credential?
                                        .GetNetworkCredential(),
                                Authentication =
                                    Authentication,
                                XPath =
                                    EventFilterCompiler
                                        .BuildXPath(
                                            new EventFilter {
                                                RecordIds =
                                                    new[] {
                                                        checkpoint
                                                    }
                                            }),
                                MaxEvents = 1,
                                RemoteConnectionTimeoutMilliseconds =
                                    EffectiveRemoteConnectionTimeoutMilliseconds,
                                RemoteReadTimeoutMilliseconds =
                                    EffectiveRemoteReadTimeoutMilliseconds,
                                BufferCapacity =
                                    BufferCapacity > 0
                                        ? BufferCapacity
                                        : 64,
                                ReadMode =
                                    EventReadMode.Metadata
                            },
                            cancellationToken)
                            .FirstOrDefault()
                        : null;
                    EvaluateCheckpointBoundary(
                        checkpointKey,
                        checkpoint,
                        boundaryEvent,
                        string.IsNullOrWhiteSpace(machine) ? log : $"{log} on {machine}");
                } catch (Exception ex) when (EventLogRemoteQueryFailureClassifier.TryClassify(machine, ex, out _)) {
                    ResetCheckpointForSafeReplay(
                        checkpointKey,
                        $"Checkpoint boundary {checkpoint} for '{checkpointKey}' could not be validated on " +
                        $"'{(string.IsNullOrWhiteSpace(machine) ? log : $"{log} on {machine}")}'. " +
                        $"Replaying this source without the saved lower bound to avoid event loss. {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    private void EvaluateCheckpointBoundary(
        string checkpointKey,
        long checkpoint,
        EventObject? boundaryEvent,
        string target) {

        if (checkpoint <= 0) {
            return;
        }

        string? actualBoundary = boundaryEvent == null
            ? null
            : EventCheckpointBoundaryIdentity.Create(boundaryEvent);
        if (!_checkpointBoundaries.TryGetValue(checkpointKey, out string? expectedBoundary)) {
            if (actualBoundary != null) {
                _checkpointBoundaryMigrations[checkpointKey] = actualBoundary;
                return;
            }
        } else if (string.Equals(expectedBoundary, actualBoundary, StringComparison.Ordinal)) {
            return;
        }

        ResetCheckpointForSafeReplay(
            checkpointKey,
            $"Checkpoint boundary {checkpoint} for '{checkpointKey}' no longer identifies the same record in '{target}'. " +
            "The source was cleared, replaced, or aged past that boundary; restarting from its oldest available matching record.");
    }

    private void ResetCheckpointForSafeReplay(string checkpointKey, string warning) {
        _recordMap.Remove(checkpointKey);
        _highestRecordIds.Remove(checkpointKey);
        _highestCheckpointEvents.Remove(checkpointKey);
        _checkpointBoundaryMigrations.Remove(checkpointKey);
        _resetCheckpointKeys.Add(checkpointKey);
        WriteWarning(warning);
    }

    private bool OutputLimitReached => MaxEvents > 0 && _eventsOutput >= MaxEvents;

    private bool UsesCheckpoint => !string.IsNullOrWhiteSpace(RecordIdFile);

    private bool EffectiveOldest => Oldest.IsPresent || UsesCheckpoint;

    /// <summary>
    /// Saves the highest contiguously processed record ID to <see cref="RecordIdFile"/> when processing completes.
    /// </summary>
    protected override Task EndProcessingAsync() {
        if (!string.IsNullOrEmpty(RecordIdFile) &&
            (_highestRecordIds.Count > 0 || _resetCheckpointKeys.Count > 0 || _checkpointBoundaryMigrations.Count > 0)) {
            var updates = new List<EventCheckpointUpdate>(
                _highestRecordIds.Count + _resetCheckpointKeys.Count + _checkpointBoundaryMigrations.Count);
            foreach (string resetKey in _resetCheckpointKeys) {
                updates.Add(new EventCheckpointUpdate(
                    resetKey,
                    _highestRecordIds.TryGetValue(resetKey, out long resetValue) ? resetValue : null,
                    GetInitialCheckpointGeneration(resetKey),
                    startsNewGeneration: true,
                    boundaryIdentity: _highestCheckpointEvents.TryGetValue(resetKey, out EventObject? resetBoundaryEvent)
                        ? EventCheckpointBoundaryIdentity.Create(resetBoundaryEvent)
                        : null));
            }
            foreach (KeyValuePair<string, long> checkpoint in _highestRecordIds) {
                if (_resetCheckpointKeys.Contains(checkpoint.Key)) {
                    continue;
                }
                updates.Add(new EventCheckpointUpdate(
                    checkpoint.Key,
                    checkpoint.Value,
                    GetInitialCheckpointGeneration(checkpoint.Key),
                    boundaryIdentity: _highestCheckpointEvents.TryGetValue(checkpoint.Key, out EventObject? boundaryEvent)
                        ? EventCheckpointBoundaryIdentity.Create(boundaryEvent)
                        : null));
            }
            foreach (KeyValuePair<string, string> migration in _checkpointBoundaryMigrations) {
                if (_resetCheckpointKeys.Contains(migration.Key) || _highestRecordIds.ContainsKey(migration.Key) ||
                    !_recordMap.TryGetValue(migration.Key, out long recordId)) {
                    continue;
                }
                updates.Add(new EventCheckpointUpdate(
                    migration.Key,
                    recordId,
                    GetInitialCheckpointGeneration(migration.Key),
                    boundaryIdentity: migration.Value));
            }

            EventCheckpointSnapshot persisted = EventCheckpointStore.Update(RecordIdFile!, updates);
            _recordMap = persisted.Records.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value,
                StringComparer.OrdinalIgnoreCase);
        }
        return Task.CompletedTask;
    }

    private Guid GetInitialCheckpointGeneration(string checkpointKey)
        => _checkpointGenerations.TryGetValue(checkpointKey, out Guid generationId)
            ? generationId
            : Guid.Empty;
}
