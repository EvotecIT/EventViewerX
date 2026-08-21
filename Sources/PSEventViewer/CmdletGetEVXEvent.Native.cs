using System.Collections;
using System.Net;

namespace PSEventViewer;

public sealed partial class CmdletGetEVXEvent {
    private void ProcessNativeEvents(
        CancellationToken cancellationToken,
        List<object>? results) {

        EventLogBatchQuery batch = CreateNativeBatch();
        foreach (EventObject eventObject in EventLogBatchEngine.Read(
                     batch,
                     cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessEventResult(eventObject, results);
            if (OutputLimitReached) {
                break;
            }
        }
    }

    private EventLogBatchQuery CreateNativeBatch() {
        if (!RequiresSpecializedBatch()) {
            return CreatePlannerBatch();
        }
        switch (ParameterSetName) {
            case "Channel":
                return CreateChannelBatch(
                    NormalizeRequiredValues(LogName, nameof(LogName)),
                    CreateCommandFilter(),
                    suppress: null,
                    FilterXPath);
            case "Path":
                return CreateFileBatch(
                    ExpandFilePaths(Path, nameof(Path)),
                    CreateCommandFilter(),
                    suppress: null,
                    FilterXPath);
            case "Provider":
                return CreateProviderBatch(
                    CreateCommandFilter(),
                    suppress: null);
            case "Hashtable":
                return CreateFilterHashtableBatch();
            case "Xml":
                return CreateStructuredBatch(
                    FilterXml!.OuterXml,
                    EventLogQuerySourceKind.Auto);
            default:
                throw new InvalidOperationException(
                    $"Parameter set '{ParameterSetName}' is not a native query parameter set.");
        }
    }

    private bool RequiresSpecializedBatch() {
        return UsesCheckpoint ||
               ParameterSetName == "Hashtable" ||
               (ParameterSetName == "Path" &&
                ContainsWildcard(
                    ResolveNativeFilter()?.ProviderNames ?? ProviderName));
    }

    private EventLogBatchQuery CreatePlannerBatch() {
        EventFilter? filter = ParameterSetName == "Xml"
            ? null
            : CreateCommandFilter();
        var definition = new EventQueryDefinition {
            LogNames = ParameterSetName == "Channel"
                ? NormalizeRequiredValues(LogName, nameof(LogName))
                : null,
            ProviderNames = ParameterSetName == "Provider"
                ? NormalizeRequiredValues(
                    ProviderName ?? Array.Empty<string>(),
                    nameof(ProviderName))
                : null,
            Paths = ParameterSetName == "Path"
                ? ExpandFilePaths(Path, nameof(Path))
                : null,
            QueryXml = ParameterSetName == "Xml"
                ? FilterXml!.OuterXml
                : null,
            MachineNames = ParameterSetName == "Path"
                ? null
                : MachineName,
            Filter = filter,
            FilterXPath = FilterXPath,
            IncludeAnalyticAndDebugChannels = Force.IsPresent,
            TolerateQueryErrors = TolerateQueryErrors.IsPresent,
            Options = new EventLogQueryOptions {
                Oldest = EffectiveOldest,
                ReadMode = ReadMode,
                MessageCulture = MessageCulture,
                FallbackMessageCulture = FallbackMessageCulture,
                MaxEvents = GetNativeCandidateLimit(),
                MaxEventsScanned = MaxEventsScanned,
                IncludeBookmark = IncludeBookmark.IsPresent,
                BookmarkXml = BookmarkXml,
                BookmarkOffset = BookmarkOffset,
                StrictBookmark = !IgnoreStaleBookmark,
                Credential = Credential?.GetNetworkCredential(),
                Authentication = Authentication,
                RemoteConnectionTimeoutMilliseconds =
                    EffectiveRemoteConnectionTimeoutMilliseconds,
                RemoteReadTimeoutMilliseconds =
                    EffectiveRemoteReadTimeoutMilliseconds,
                BufferCapacity = BufferCapacity > 0
                    ? BufferCapacity
                    : 64,
                MaxConcurrency = DisableParallel.IsPresent
                    ? 1
                    : MaxConcurrency,
                ContinueOnError = ContinueOnError.IsPresent,
                FailureHandler = failure => WriteError(new ErrorRecord(
                    failure.Exception,
                    "EVXEventQuerySourceFailed",
                    ErrorCategory.ReadError,
                    string.IsNullOrWhiteSpace(failure.MachineName)
                        ? failure.Source
                        : $"{failure.Source} on {failure.MachineName}"))
            }
        };
        return EventQueryPlanner.CreateBatch(definition, CancelToken);
    }

    private EventLogBatchQuery CreateFilterHashtableBatch() {
        Hashtable[] hashtables = GetFilterHashtables();
        PowerShellEventFilterBinding[] bindings =
            hashtables
                .Select(PowerShellEventFilterAdapter.Bind)
                .ToArray();
        bool usesFiles =
            bindings.Any(static binding =>
                binding.UsesFiles);
        bool usesRemoteCapableSource =
            bindings.Any(static binding =>
                binding.UsesChannels ||
                binding.ProviderOnly);
        if (usesFiles &&
            !usesRemoteCapableSource &&
            (MyInvocation.BoundParameters.ContainsKey(
                 nameof(MachineName)) ||
             Credential != null)) {
            throw new PSArgumentException(
                "MachineName and Credential cannot be used with a file-only FilterHashtable query. Offline event-log files are always read locally.");
        }

        var batches = new List<EventLogBatchQuery>();
        foreach (PowerShellEventFilterBinding binding in
                 bindings) {
            if (binding.ProviderOnly) {
                batches.Add(CreateProviderBatch(
                    binding.Select,
                    binding.Suppress));
                continue;
            }
            if (binding.UsesChannels) {
                batches.Add(CreateChannelBatch(
                    binding.LogNames,
                    binding.Select,
                    binding.Suppress,
                    rawXPath: null));
            }
            if (binding.UsesFiles) {
                batches.Add(CreateFileBatch(
                    ExpandFilePaths(
                        binding.Paths,
                        nameof(FilterHashtable)),
                    binding.Select,
                    binding.Suppress,
                    rawXPath: null,
                    allowManagedProviderFilter:
                        bindings.Length == 1,
                    allowRemoteBatchContext:
                        usesRemoteCapableSource));
            }
        }
        EventLogBatchQuery batch = batches.Count == 1
            ? batches[0]
            : EventLogBatchQuery.Combine(batches);
        batch = ConsolidateAndValidateBookmarkFanOut(batch);
        ConfigureBatch(batch);
        return batch;
    }

    private Hashtable[] GetFilterHashtables() {
        Hashtable[] hashtables = FilterHashtable?
            .Where(static table => table != null)
            .ToArray() ?? Array.Empty<Hashtable>();
        if (hashtables.Length == 0) {
            throw new PSArgumentException(
                "FilterHashtable requires at least one hashtable.");
        }
        return hashtables;
    }

    private EventLogBatchQuery CreateProviderBatch(
        EventFilter filter,
        EventFilter? suppress) {

        EventFilterCompiler.SplitNamedDataExclusions(
            filter,
            out EventFilter? selectFilter,
            out EventFilter? namedDataSuppression);
        filter = selectFilter!;
        IReadOnlyList<EventFilter> namedDataSuppressions =
            PartitionSuppressions(namedDataSuppression);
        string[] providerPatterns = NormalizeRequiredValues(
            filter.ProviderNames ?? Array.Empty<string>(),
            nameof(ProviderName));
        IReadOnlyList<string?> machines =
            EventLogTarget.NormalizeMachineNames(MachineName);
        ValidateRemoteCredentialTargets(machines);
        var channels = new List<EventLogChannelQuery>();
        var structured = new List<EventLogStructuredQuery>();
        foreach (string? machine in machines) {
            var catalogQuery = new EventLogCatalogQuery {
                MachineName = machine,
                Credential = Credential?.GetNetworkCredential(),
                Authentication = Authentication,
                ConnectionTimeoutMilliseconds =
                    EffectiveRemoteConnectionTimeoutMilliseconds,
                Culture = MessageCulture
            };
            EventProviderCatalogResult[] providers;
            try {
                providers = EventLogCatalog
                    .GetProviders(
                        catalogQuery,
                        providerPatterns,
                        CancelToken)
                    .ToArray();
            } catch (Exception exception) {
                if (!ContinueOnError) {
                    throw;
                }
                WriteError(new ErrorRecord(
                    exception,
                    "EVXProviderDiscoveryFailed",
                    ErrorCategory.ResourceUnavailable,
                    machine));
                continue;
            }
            foreach (EventProviderCatalogResult failure in providers
                         .Where(static result => !result.Success)) {
                if (!ContinueOnError) {
                    throw failure.Exception!;
                }
                WriteError(new ErrorRecord(
                    failure.Exception!,
                    "EVXProviderMetadataFailed",
                    ErrorCategory.ReadError,
                    failure.ProviderName));
            }

            EventProviderMetadataSnapshot[] successful = providers
                .Where(static result => result.Success)
                .Select(static result => result.Provider!)
                .ToArray();
            EventFilter? machineSuppress = suppress;
            if (suppress != null &&
                ContainsWildcard(suppress.ProviderNames)) {
                machineSuppress = ExpandProviderPatterns(
                    suppress,
                    machine,
                    suppressFilter: true);
            }
            IReadOnlyList<EventFilter> suppressions =
                PartitionSuppressions(machineSuppress);
            if (namedDataSuppressions.Count > 0) {
                suppressions = suppressions
                    .Concat(namedDataSuppressions)
                    .ToArray();
            }
            foreach (IGrouping<string, EventProviderMetadataSnapshot> group in
                     successful
                         .SelectMany(
                             provider => provider.LogLinks,
                             static (provider, link) => new {
                                 Provider = provider,
                                 Link = link
                             })
                         .Where(static item =>
                             !string.IsNullOrWhiteSpace(item.Link.LogName))
                         .GroupBy(
                             static item => item.Link.LogName,
                             static item => item.Provider,
                             StringComparer.OrdinalIgnoreCase)) {
                if (!Force.IsPresent &&
                    providerPatterns.Any(
                        WildcardPattern
                            .ContainsWildcardCharacters) &&
                    EventLogCatalog.GetChannelNames(
                        catalogQuery,
                        new[] { group.Key },
                        includeAnalyticDebug: false,
                        cancellationToken: CancelToken)
                    .Count == 0) {
                    continue;
                }
                EventFilter sourceFilter = WithProviders(
                    CopyFilterWithCheckpoint(
                        filter,
                        machine,
                        group.Key),
                    group
                        .Select(static provider => provider.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray());
                IReadOnlyList<EventFilter> partitions =
                    EventFilterPartitioner.Partition(
                        sourceFilter);
                string[] partitionQueries =
                    suppressions.Count == 0
                        ? partitions
                            .Select(
                                EventFilterCompiler
                                    .BuildXPath)
                            .ToArray()
                        : partitions
                            .Select(partition =>
                                EventFilterCompiler
                                    .BuildChannelQueryXmlWithSuppressions(
                                        new[] {
                                            group.Key
                                        },
                                        partition,
                                        suppressions))
                            .ToArray();
                string? batchSourceIdentity =
                    CreateBatchSourceIdentity(
                        partitionQueries);
                foreach (string partitionQuery in
                         partitionQueries) {
                    if (suppressions.Count == 0) {
                        channels.Add(CreateChannelQuery(
                            group.Key,
                            machine,
                            partitionQuery,
                            batchSourceIdentity));
                    } else {
                        structured.Add(CreateStructuredQuery(
                            partitionQuery,
                            EventLogQuerySourceKind.Channel,
                            machine,
                            batchSourceIdentity));
                    }
                }
            }
        }

        if (channels.Count + structured.Count == 0) {
            throw new ItemNotFoundException(
                $"No event channels are linked to provider pattern(s): {string.Join(", ", providerPatterns)}.");
        }
        var batches = new List<EventLogBatchQuery>();
        if (channels.Count > 0) {
            batches.Add(EventLogBatchQuery.ForChannels(channels));
        }
        if (structured.Count > 0) {
            batches.Add(EventLogBatchQuery.ForStructured(structured));
        }
        EventLogBatchQuery batch = batches.Count == 1
            ? batches[0]
            : EventLogBatchQuery.Combine(batches);
        batch = ConsolidateAndValidateBookmarkFanOut(batch);
        ConfigureBatch(batch);
        return batch;
    }

    private EventLogBatchQuery CreateChannelBatch(
        IReadOnlyList<string> logNames,
        EventFilter filter,
        EventFilter? suppress,
        string? rawXPath) {

        IReadOnlyList<string?> machines =
            EventLogTarget.NormalizeMachineNames(MachineName);
        ValidateRemoteCredentialTargets(machines);
        ValidateRawXPathCombination(rawXPath, filter);
        EventFilterCompiler
            .SplitNamedDataExclusions(
                filter,
                out EventFilter? selectFilter,
                out EventFilter? namedDataSuppression);
        filter = selectFilter!;
        IReadOnlyList<EventFilter>
            namedDataSuppressions =
                PartitionSuppressions(
                    namedDataSuppression);
        bool useStructured =
            !UsesCheckpoint &&
            string.IsNullOrWhiteSpace(rawXPath) &&
            (namedDataSuppressions.Count > 0 ||
             suppress != null ||
             logNames.Count > 1 ||
             (filter.ProviderNames?.Count ?? 0) > 0);
        var channels = new List<EventLogChannelQuery>();
        var structured = new List<EventLogStructuredQuery>();
        foreach (string? machine in machines) {
            IReadOnlyList<string> machineLogNames =
                ExpandChannelPatterns(logNames, machine);
            EventFilter machineFilter = ContainsWildcard(
                    filter.ProviderNames)
                ? ExpandProviderPatterns(
                    filter,
                    machine,
                    suppressFilter: false)
                : filter;
            EventFilter? machineSuppress = suppress;
            if (suppress != null &&
                ContainsWildcard(suppress.ProviderNames)) {
                machineSuppress = ExpandProviderPatterns(
                    suppress,
                    machine,
                    suppressFilter: true);
            }
            IReadOnlyList<EventFilter> suppressions =
                PartitionSuppressions(machineSuppress);
            if (namedDataSuppressions.Count > 0) {
                suppressions = suppressions
                    .Concat(namedDataSuppressions)
                    .ToArray();
            }

            if (!string.IsNullOrWhiteSpace(rawXPath)) {
                foreach (string logName in machineLogNames) {
                    channels.Add(CreateChannelQuery(
                        logName,
                        machine,
                        rawXPath!));
                }
                continue;
            }

            if (!UsesCheckpoint) {
                IReadOnlyList<EventFilter> chunks =
                    EventFilterPartitioner.Partition(
                        machineFilter);
                if (useStructured) {
                    string[] partitionQueries =
                        chunks
                            .Select(chunk =>
                                EventFilterCompiler
                                    .BuildChannelQueryXmlWithSuppressions(
                                        machineLogNames,
                                        chunk,
                                        suppressions))
                            .ToArray();
                    string? batchSourceIdentity =
                        CreateBatchSourceIdentity(
                            partitionQueries);
                    foreach (string partitionQuery in
                             partitionQueries) {
                        structured.Add(CreateStructuredQuery(
                            partitionQuery,
                            EventLogQuerySourceKind.Channel,
                            machine,
                            batchSourceIdentity));
                    }
                    continue;
                }
            }

            foreach (string logName in machineLogNames) {
                EventFilter sourceFilter = CopyFilterWithCheckpoint(
                    machineFilter,
                    machine,
                    logName);
                if (string.Equals(
                        logName,
                        "ForwardedEvents",
                        StringComparison.OrdinalIgnoreCase) &&
                    suppressions.Count == 0) {
                    EventLogBatchQuery managed =
                        EventLogQueryFactory.ForChannels(
                            new[] { logName },
                            new[] { machine },
                            sourceFilter,
                            CreateChannelFactoryOptions(machine));
                    channels.AddRange(managed.ChannelQueries);
                    continue;
                }
                IReadOnlyList<EventFilter> chunks =
                    EventFilterPartitioner.Partition(
                        sourceFilter);
                string[] partitionQueries =
                    suppressions.Count > 0
                        ? chunks
                            .Select(chunk =>
                                EventFilterCompiler
                                    .BuildChannelQueryXmlWithSuppressions(
                                        new[] {
                                            logName
                                        },
                                        chunk,
                                        suppressions))
                            .ToArray()
                        : chunks
                            .Select(
                                EventFilterCompiler
                                    .BuildXPath)
                            .ToArray();
                string? batchSourceIdentity =
                    CreateBatchSourceIdentity(
                        partitionQueries);
                foreach (string partitionQuery in
                         partitionQueries) {
                    if (suppressions.Count > 0) {
                        structured.Add(CreateStructuredQuery(
                            partitionQuery,
                            EventLogQuerySourceKind.Channel,
                            machine,
                            batchSourceIdentity));
                    } else {
                        channels.Add(CreateChannelQuery(
                            logName,
                            machine,
                            partitionQuery,
                            batchSourceIdentity));
                    }
                }
            }
        }
        EventLogBatchQuery batch = structured.Count > 0
            ? EventLogBatchQuery.ForStructured(structured)
            : EventLogBatchQuery.ForChannels(channels);
        batch = ConsolidateAndValidateBookmarkFanOut(batch);
        ConfigureBatch(batch);
        return batch;
    }

    private EventLogQueryOptions CreateChannelFactoryOptions(
        string? machineName) {

        return new EventLogQueryOptions {
            Oldest = EffectiveOldest,
            ReadMode = ReadMode,
            MessageCulture = MessageCulture,
            FallbackMessageCulture = FallbackMessageCulture,
            MaxEvents = GetNativeCandidateLimit(),
            MaxEventsScanned = MaxEventsScanned,
            IncludeBookmark = IncludeBookmark,
            BookmarkXml = BookmarkXml,
            BookmarkOffset = BookmarkOffset,
            StrictBookmark = !IgnoreStaleBookmark,
            Credential = EventLogTarget.IsLocalMachine(machineName)
                ? null
                : Credential?.GetNetworkCredential(),
            Authentication = Authentication,
            RemoteConnectionTimeoutMilliseconds =
                EffectiveRemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds =
                EffectiveRemoteReadTimeoutMilliseconds,
            BufferCapacity = BufferCapacity > 0 ? BufferCapacity : 64,
            MaxConcurrency = DisableParallel.IsPresent ? 1 : MaxConcurrency,
            ContinueOnError = ContinueOnError.IsPresent
        };
    }

    private EventFilter ExpandProviderPatterns(
        EventFilter filter,
        string? machineName,
        bool suppressFilter) {

        string[] patterns = NormalizeRequiredValues(
            filter.ProviderNames ?? Array.Empty<string>(),
            nameof(ProviderName));
        var catalogQuery = new EventLogCatalogQuery {
            MachineName = machineName,
            Credential = Credential?.GetNetworkCredential(),
            Authentication = Authentication,
            ConnectionTimeoutMilliseconds =
                EffectiveRemoteConnectionTimeoutMilliseconds,
            Culture = MessageCulture
        };
        string[] providers = EventLogCatalog
            .GetProviderNames(
                catalogQuery,
                patterns,
                CancelToken)
            .ToArray();
        if (providers.Length == 0) {
            if (!suppressFilter) {
                throw new ItemNotFoundException(
                    $"No event providers match pattern(s) '{string.Join(", ", patterns)}' on '{machineName ?? Environment.MachineName}'.");
            }
            providers = new[] {
                "__EventViewerX_No_Matching_Provider__"
            };
        }
        return WithProviders(filter, providers);
    }

    private static bool ContainsWildcard(
        IReadOnlyList<string>? values) {

        return values?.Any(static value =>
            value.IndexOf('*') >= 0 ||
            value.IndexOf('?') >= 0) == true;
    }


}
