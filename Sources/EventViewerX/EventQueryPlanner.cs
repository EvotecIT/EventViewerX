namespace EventViewerX;

/// <summary>Resolves high-level event sources and builds one native, bounded query batch.</summary>
public static class EventQueryPlanner {
    /// <summary>Builds a reusable native query batch from a high-level definition.</summary>
    public static EventLogBatchQuery CreateBatch(
        EventQueryDefinition definition,
        CancellationToken cancellationToken = default) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        EventLogQueryOptions options = definition.Options ??
            throw new ArgumentException("Options cannot be null.", nameof(definition));
        bool structuredQueryUsesFiles = ValidateSource(definition);
        ValidateCredentialTargets(
            definition,
            options,
            structuredQueryUsesFiles);
        if (definition.Filter?.HasAny == true && !string.IsNullOrWhiteSpace(definition.FilterXPath)) {
            throw new ArgumentException("Filter and FilterXPath cannot be combined.", nameof(definition));
        }
        if (HasValues(definition.ProviderNames) && !string.IsNullOrWhiteSpace(definition.FilterXPath)) {
            throw new ArgumentException(
                "ProviderNames and FilterXPath are separate source modes and cannot be combined. " +
                "Put the provider predicate in FilterXPath or use the typed Filter.",
                nameof(definition));
        }

        EventLogBatchQuery batch;
        if (HasValues(definition.LogNames)) {
            batch = CreateChannelBatch(definition, options, cancellationToken);
        } else if (HasValues(definition.ProviderNames)) {
            batch = CreateProviderBatch(definition, options, cancellationToken);
        } else if (HasValues(definition.Paths)) {
            batch = CreateFileBatch(definition, options);
        } else {
            batch = CreateStructuredBatch(definition, options);
        }
        ValidateBookmarkFanOut(batch, options);
        return batch;
    }

    private static EventLogBatchQuery CreateChannelBatch(
        EventQueryDefinition definition,
        EventLogQueryOptions options,
        CancellationToken cancellationToken) {

        var batches = new List<EventLogBatchQuery>();
        foreach (string? machine in NormalizeMachines(definition.MachineNames)) {
            cancellationToken.ThrowIfCancellationRequested();
            var catalog = CreateCatalogQuery(machine, options);
            string[] channels = EventLogCatalog.GetChannelNames(
                    catalog,
                    definition.LogNames,
                    definition.IncludeAnalyticAndDebugChannels,
                    cancellationToken)
                .ToArray();
            if (channels.Length == 0) {
                throw new ArgumentException("The channel patterns did not match any event logs.", nameof(definition));
            }
            EventFilter filter = ResolveProviderPatterns(
                definition.Filter,
                catalog,
                cancellationToken);
            EventLogQueryOptions targetOptions = CopyOptions(options, machine);
            batches.Add(string.IsNullOrWhiteSpace(definition.FilterXPath)
                ? EventLogQueryFactory.ForChannels(channels, new[] { machine }, filter, targetOptions)
                : CreateRawChannelBatch(channels, machine, definition.FilterXPath!, targetOptions));
        }
        return Combine(batches);
    }

    private static EventLogBatchQuery CreateProviderBatch(
        EventQueryDefinition definition,
        EventLogQueryOptions options,
        CancellationToken cancellationToken) {

        var batches = new List<EventLogBatchQuery>();
        foreach (string? machine in NormalizeMachines(definition.MachineNames)) {
            cancellationToken.ThrowIfCancellationRequested();
            var catalog = CreateCatalogQuery(machine, options);
            string[] providers = EventLogCatalog.GetProviderNames(
                    catalog,
                    definition.ProviderNames,
                    cancellationToken)
                .ToArray();
            if (providers.Length == 0) {
                throw new ArgumentException("The provider patterns did not match any registered providers.", nameof(definition));
            }
            string[] channels = EventLogCatalog.ResolveProviderChannels(
                    catalog,
                    providers,
                    cancellationToken)
                .ToArray();
            if (channels.Length == 0) {
                throw new ArgumentException("The matching providers are not linked to any event channels.", nameof(definition));
            }
            EventFilter filter = definition.Filter?.Clone() ?? new EventFilter();
            filter.ProviderNames = providers;
            EventLogQueryOptions targetOptions = CopyOptions(options, machine);
            batches.Add(EventLogQueryFactory.ForChannels(
                channels,
                new[] { machine },
                filter,
                targetOptions));
        }
        return Combine(batches);
    }

    private static EventLogBatchQuery CreateFileBatch(
        EventQueryDefinition definition,
        EventLogQueryOptions options) {

        string[] paths = ExpandPaths(definition.Paths!);
        if (paths.Length == 0) {
            throw new FileNotFoundException("The supplied event-log paths did not match any files.");
        }
        return string.IsNullOrWhiteSpace(definition.FilterXPath)
            ? EventLogQueryFactory.ForFiles(paths, definition.Filter, CopyOptions(options, null))
            : CreateRawFileBatch(paths, definition.FilterXPath!, CopyOptions(options, null));
    }

    private static EventLogBatchQuery CreateStructuredBatch(
        EventQueryDefinition definition,
        EventLogQueryOptions options) {

        string queryXml = definition.QueryXml!.Trim();
        var queries = NormalizeMachines(definition.MachineNames)
            .Select(machine => Configure(
                new EventLogStructuredQuery(queryXml) {
                    SourceKind = EventLogQuerySourceKind.Auto,
                    MachineName = EventLogTarget.IsLocalMachine(machine) ? null : machine,
                    TolerateQueryErrors = definition.TolerateQueryErrors
                },
                CopyOptions(options, machine)))
            .ToArray();
        return Finalize(EventLogBatchQuery.ForStructured(queries), options);
    }

    private static EventLogBatchQuery CreateRawChannelBatch(
        IEnumerable<string> channels,
        string? machine,
        string xpath,
        EventLogQueryOptions options) {

        string normalizedXPath = NormalizeXPath(xpath);
        EventLogChannelQuery[] queries = channels.Select(channel => Configure(
            new EventLogChannelQuery(channel) {
                MachineName = EventLogTarget.IsLocalMachine(machine) ? null : machine,
                Credential = EventLogTarget.IsLocalMachine(machine) ? null : options.Credential,
                Authentication = options.Authentication,
                XPath = normalizedXPath
            },
            options)).ToArray();
        return Finalize(EventLogBatchQuery.ForChannels(queries), options);
    }

    private static EventLogBatchQuery CreateRawFileBatch(
        IEnumerable<string> paths,
        string xpath,
        EventLogQueryOptions options) {

        if (options.Credential != null) {
            throw new ArgumentException("Offline file queries cannot use credentials.", nameof(options));
        }
        string normalizedXPath = NormalizeXPath(xpath);
        EventLogFileQuery[] queries = paths.Select(path => Configure(
            new EventLogFileQuery(path) { XPath = normalizedXPath },
            options)).ToArray();
        return Finalize(EventLogBatchQuery.ForFiles(queries), options);
    }

    private static EventLogBatchQuery Combine(IReadOnlyList<EventLogBatchQuery> batches) {
        if (batches.Count == 1) {
            return batches[0];
        }
        return EventLogBatchConsolidator.Consolidate(EventLogBatchQuery.Combine(batches));
    }

    private static EventLogBatchQuery Finalize(EventLogBatchQuery batch, EventLogQueryOptions options) {
        batch.MaxEvents = options.MaxEvents;
        batch.MaxConcurrency = options.MaxConcurrency;
        batch.ContinueOnError = options.ContinueOnError;
        batch.FailureHandler = options.FailureHandler;
        return EventLogBatchConsolidator.Consolidate(batch);
    }

    private static EventLogChannelQuery Configure(EventLogChannelQuery query, EventLogQueryOptions options) {
        query.Oldest = options.Oldest;
        query.ReadMode = options.ReadMode;
        query.MessageCulture = options.MessageCulture;
        query.FallbackMessageCulture = options.FallbackMessageCulture;
        query.MaxEvents = options.MaxEvents;
        query.IncludeBookmark = options.IncludeBookmark;
        query.BookmarkXml = options.BookmarkXml;
        query.BookmarkOffset = options.BookmarkOffset;
        query.StrictBookmark = options.StrictBookmark;
        query.RemoteConnectionTimeoutMilliseconds = options.RemoteConnectionTimeoutMilliseconds;
        query.RemoteReadTimeoutMilliseconds = options.RemoteReadTimeoutMilliseconds;
        query.BufferCapacity = options.BufferCapacity;
        query.RpcEndpointPort = options.RpcEndpointPort;
        return query;
    }

    private static EventLogFileQuery Configure(EventLogFileQuery query, EventLogQueryOptions options) {
        query.Oldest = options.Oldest;
        query.ReadMode = options.ReadMode;
        query.MessageCulture = options.MessageCulture;
        query.FallbackMessageCulture = options.FallbackMessageCulture;
        query.MaxEvents = options.MaxEvents;
        query.IncludeBookmark = options.IncludeBookmark;
        query.BookmarkXml = options.BookmarkXml;
        query.BookmarkOffset = options.BookmarkOffset;
        query.StrictBookmark = options.StrictBookmark;
        return query;
    }

    private static EventLogStructuredQuery Configure(EventLogStructuredQuery query, EventLogQueryOptions options) {
        query.Credential = EventLogTarget.IsLocalMachine(query.MachineName) ? null : options.Credential;
        query.Authentication = options.Authentication;
        query.Oldest = options.Oldest;
        query.ReadMode = options.ReadMode;
        query.MessageCulture = options.MessageCulture;
        query.FallbackMessageCulture = options.FallbackMessageCulture;
        query.MaxEvents = options.MaxEvents;
        query.IncludeBookmark = options.IncludeBookmark;
        query.BookmarkXml = options.BookmarkXml;
        query.BookmarkOffset = options.BookmarkOffset;
        query.StrictBookmark = options.StrictBookmark;
        query.RemoteConnectionTimeoutMilliseconds = options.RemoteConnectionTimeoutMilliseconds;
        query.RemoteReadTimeoutMilliseconds = options.RemoteReadTimeoutMilliseconds;
        query.BufferCapacity = options.BufferCapacity;
        query.RpcEndpointPort = options.RpcEndpointPort;
        return query;
    }

    private static EventLogCatalogQuery CreateCatalogQuery(string? machine, EventLogQueryOptions options) {
        return new EventLogCatalogQuery {
            MachineName = EventLogTarget.IsLocalMachine(machine) ? null : machine,
            Credential = EventLogTarget.IsLocalMachine(machine) ? null : options.Credential,
            Authentication = options.Authentication,
            ConnectionTimeoutMilliseconds = options.RemoteConnectionTimeoutMilliseconds,
            Culture = options.MessageCulture
        };
    }

    private static EventLogQueryOptions CopyOptions(EventLogQueryOptions options, string? machine) {
        return new EventLogQueryOptions {
            Oldest = options.Oldest,
            ReadMode = options.ReadMode,
            MessageCulture = options.MessageCulture,
            FallbackMessageCulture = options.FallbackMessageCulture,
            MaxEvents = options.MaxEvents,
            MaxEventsScanned = options.MaxEventsScanned,
            IncludeBookmark = options.IncludeBookmark,
            BookmarkXml = options.BookmarkXml,
            BookmarkOffset = options.BookmarkOffset,
            StrictBookmark = options.StrictBookmark,
            Credential = EventLogTarget.IsLocalMachine(machine) ? null : options.Credential,
            Authentication = options.Authentication,
            RemoteConnectionTimeoutMilliseconds = options.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = options.RemoteReadTimeoutMilliseconds,
            BufferCapacity = options.BufferCapacity,
            RpcEndpointPort = options.RpcEndpointPort,
            MaxConcurrency = options.MaxConcurrency,
            ContinueOnError = options.ContinueOnError,
            FailureHandler = options.FailureHandler
        };
    }

    private static EventFilter ResolveProviderPatterns(
        EventFilter? source,
        EventLogCatalogQuery catalog,
        CancellationToken cancellationToken) {

        EventFilter filter = source?.Clone() ?? new EventFilter();
        if (filter.ProviderNames?.Any(ContainsWildcard) != true) {
            return filter;
        }
        string[] providers = EventLogCatalog.GetProviderNames(
                catalog,
                filter.ProviderNames,
                cancellationToken)
            .ToArray();
        if (providers.Length == 0) {
            throw new ArgumentException(
                "The provider patterns in Filter did not match any registered providers.",
                nameof(source));
        }
        filter.ProviderNames = providers;
        return filter;
    }

    private static bool ContainsWildcard(string value) {
        return value?.IndexOf('*') >= 0 || value?.IndexOf('?') >= 0;
    }

    private static string[] ExpandPaths(IReadOnlyList<string> paths) {
        var output = new HashSet<string>(FileSystemPathIdentity.Comparer);
        foreach (string value in FileSystemPathIdentity.NormalizeUnresolvedPaths(paths)) {
            bool containsWildcard = FileSystemPathIdentity.ContainsWildcard(value);
            if (!containsWildcard) {
                string fullPath = FileSystemPathIdentity.GetFullPath(value);
                if (Directory.Exists(fullPath)) {
                    foreach (string file in Directory.EnumerateFiles(fullPath, "*.evtx", SearchOption.TopDirectoryOnly)) {
                        output.Add(FileSystemPathIdentity.GetFullPath(file));
                    }
                    continue;
                }
                if (!File.Exists(fullPath)) {
                    throw new FileNotFoundException($"Event log file '{fullPath}' was not found.", fullPath);
                }
                output.Add(fullPath);
                continue;
            }
            foreach (string file in ExpandFilePattern(value)) {
                output.Add(file);
            }
        }
        return output.OrderBy(static path => path, FileSystemPathIdentity.Comparer).ToArray();
    }

    private static IEnumerable<string> ExpandFilePattern(string pattern) {
        string root = Path.GetPathRoot(pattern) ?? string.Empty;
        if (FileSystemPathIdentity.ContainsWildcard(root)) {
            throw new ArgumentException(
                "Wildcards are not supported in a path root, UNC server name, or UNC share name.",
                nameof(pattern));
        }
        string remainder = pattern.Substring(root.Length);
        string[] segments = remainder.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) {
            yield break;
        }

        var directories = new HashSet<string>(FileSystemPathIdentity.Comparer) {
            string.IsNullOrEmpty(root)
                ? Directory.GetCurrentDirectory()
                : FileSystemPathIdentity.GetFullPath(root)
        };
        for (int index = 0; index < segments.Length - 1; index++) {
            string segment = segments[index];
            var next = new HashSet<string>(FileSystemPathIdentity.Comparer);
            foreach (string directory in directories) {
                if (FileSystemPathIdentity.ContainsWildcard(segment)) {
                    if (!Directory.Exists(directory)) {
                        continue;
                    }
                    foreach (string match in Directory.EnumerateDirectories(
                                 directory,
                                 segment,
                                 SearchOption.TopDirectoryOnly)) {
                        next.Add(FileSystemPathIdentity.GetFullPath(match));
                    }
                    continue;
                }

                string candidate = FileSystemPathIdentity.GetFullPath(
                    Path.Combine(directory, segment));
                if (Directory.Exists(candidate)) {
                    next.Add(candidate);
                }
            }
            directories = next;
            if (directories.Count == 0) {
                yield break;
            }
        }

        string filePattern = segments[segments.Length - 1];
        foreach (string directory in directories) {
            foreach (string file in Directory.EnumerateFiles(
                         directory,
                         filePattern,
                         SearchOption.TopDirectoryOnly)) {
                yield return FileSystemPathIdentity.GetFullPath(file);
            }
        }
    }

    private static string?[] NormalizeMachines(IReadOnlyList<string?>? machines) {
        return EventLogTarget.NormalizeMachineNames(machines).ToArray();
    }

    private static string NormalizeXPath(string xpath) {
        string normalized = xpath?.Trim() ?? string.Empty;
        if (normalized.Length == 0) {
            throw new ArgumentException("FilterXPath cannot be empty.", nameof(xpath));
        }
        return normalized;
    }

    private static bool ValidateSource(EventQueryDefinition definition) {
        int sourceCount = (HasValues(definition.LogNames) ? 1 : 0) +
                          (HasValues(definition.Paths) ? 1 : 0) +
                          (HasValues(definition.ProviderNames) ? 1 : 0) +
                          (!string.IsNullOrWhiteSpace(definition.QueryXml) ? 1 : 0);
        if (sourceCount != 1) {
            throw new ArgumentException(
                "Exactly one of LogNames, Paths, ProviderNames, or QueryXml must be supplied.",
                nameof(definition));
        }
        if (!string.IsNullOrWhiteSpace(definition.QueryXml) &&
            (definition.Filter?.HasAny == true || !string.IsNullOrWhiteSpace(definition.FilterXPath))) {
            throw new ArgumentException("QueryXml cannot be combined with Filter or FilterXPath.", nameof(definition));
        }
        bool structuredQueryUsesFiles =
            !string.IsNullOrWhiteSpace(definition.QueryXml) &&
            new EventLogStructuredQuery(definition.QueryXml!)
                .ResolveSources()
                .Any(static source =>
                    source.Kind == EventLogQuerySourceKind.File);
        if ((HasValues(definition.Paths) || structuredQueryUsesFiles) &&
            HasExplicitMachineTargets(definition.MachineNames)) {
            throw new ArgumentException("Offline Paths cannot be combined with MachineNames.", nameof(definition));
        }
        return structuredQueryUsesFiles;
    }

    private static void ValidateCredentialTargets(
        EventQueryDefinition definition,
        EventLogQueryOptions options,
        bool structuredQueryUsesFiles) {

        if (options.Credential == null) {
            return;
        }
        if (HasValues(definition.Paths) ||
            structuredQueryUsesFiles ||
            NormalizeMachines(definition.MachineNames)
                .Any(EventLogTarget.IsLocalMachine)) {
            throw new ArgumentException(
                "Credential can only be used when every query target is a remote computer.",
                nameof(definition));
        }
    }

    private static void ValidateBookmarkFanOut(EventLogBatchQuery batch, EventLogQueryOptions options) {
        if (string.IsNullOrWhiteSpace(options.BookmarkXml)) {
            return;
        }
        int sources = batch.ChannelQueries.Count + batch.FileQueries.Count + batch.StructuredQueries.Count;
        if (sources != 1) {
            throw new ArgumentException("BookmarkXml requires exactly one native query source.", nameof(options));
        }
    }

    private static bool HasValues<T>(IReadOnlyList<T>? values) {
        return values != null && values.Count > 0;
    }

    private static bool HasExplicitMachineTargets(
        IReadOnlyList<string?>? machineNames) {

        return machineNames != null &&
               machineNames.Any(static machine =>
                   !string.IsNullOrWhiteSpace(machine));
    }
}
