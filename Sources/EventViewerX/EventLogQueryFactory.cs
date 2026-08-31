namespace EventViewerX;

/// <summary>
/// Builds partition-safe native query batches from typed filters.
/// </summary>
public static class EventLogQueryFactory {
    /// <summary>
    /// Builds a consolidated local or remote channel batch. Large filters are partitioned
    /// within the native 22-expression limit and overlapping selects are deduplicated by Windows.
    /// </summary>
    public static EventLogBatchQuery ForChannels(
        IEnumerable<string> logNames,
        IEnumerable<string?>? machineNames = null,
        EventFilter? filter = null,
        EventLogQueryOptions? options = null) {

        string[] logs = NormalizeSources(
            logNames,
            nameof(logNames),
            StringComparer.OrdinalIgnoreCase);
        string?[] machines = NormalizeMachines(
            machineNames);
        EventLogQueryOptions snapshot =
            SnapshotAndValidate(options);
        if (logs.Any(static logName =>
                string.Equals(
                    logName,
                    ForwardedEventsQuerySafety.ChannelName,
                    StringComparison.OrdinalIgnoreCase))) {
            var batches = new List<EventLogBatchQuery>(logs.Length);
            foreach (string logName in logs) {
                batches.Add(string.Equals(
                        logName,
                        ForwardedEventsQuerySafety.ChannelName,
                        StringComparison.OrdinalIgnoreCase)
                    ? CreateManagedForwardedBatch(
                        machines,
                        filter,
                        snapshot)
                    : ForChannels(
                        new[] { logName },
                        machines,
                        filter,
                        snapshot));
            }
            return EventLogBatchQuery.Combine(batches);
        }
        EventFilter? namedDataSuppression =
            EventFilterCompiler
                .CreateExcludedNamedDataSuppression(
                    filter);
        IReadOnlyList<EventFilter> suppressionPartitions =
            EventFilterPartitioner
                .PartitionNamedDataSuppression(
                    namedDataSuppression);
        EventFilter[] filters = Partition(
            EventFilterCompiler
                .WithoutExcludedNamedData(filter));
        if (namedDataSuppression != null) {
            var structured =
                new List<EventLogStructuredQuery>(
                    checked(
                        machines.Length *
                        filters.Length));
            foreach (string? machine in machines) {
                foreach (EventFilter partition in filters) {
                    structured.Add(
                        CreateStructuredQuery(
                            EventFilterCompiler
                                .BuildChannelQueryXmlWithSuppressions(
                                    logs,
                                    partition,
                                    suppressionPartitions),
                            EventLogQuerySourceKind.Channel,
                            machine,
                            snapshot));
                }
            }
            return Finalize(
                EventLogBatchQuery.ForStructured(
                    structured),
                snapshot);
        }
        var queries =
            new List<EventLogChannelQuery>(
                checked(
                    logs.Length *
                    machines.Length *
                    filters.Length));
        foreach (string? machine in machines) {
            bool local =
                EventLogTarget.IsLocalMachine(
                    machine);
            foreach (string logName in logs) {
                foreach (EventFilter partition in
                         filters) {
                    queries.Add(
                        new EventLogChannelQuery(
                            logName) {
                            MachineName =
                                local ? null : machine,
                            Credential =
                                local
                                    ? null
                                    : snapshot.Credential,
                            Authentication =
                                snapshot.Authentication,
                            XPath =
                                EventFilterCompiler.BuildXPath(
                                    partition),
                            Oldest = snapshot.Oldest,
                            ReadMode =
                                snapshot.ReadMode,
                            MessageCulture =
                                snapshot.MessageCulture,
                            FallbackMessageCulture =
                                snapshot.FallbackMessageCulture,
                            IncludeBookmark =
                                snapshot.IncludeBookmark,
                            BookmarkXml = snapshot.BookmarkXml,
                            BookmarkOffset = snapshot.BookmarkOffset,
                            StrictBookmark = snapshot.StrictBookmark,
                            RemoteConnectionTimeoutMilliseconds =
                                snapshot.RemoteConnectionTimeoutMilliseconds,
                            RemoteReadTimeoutMilliseconds =
                                snapshot.RemoteReadTimeoutMilliseconds,
                            BufferCapacity =
                                snapshot.BufferCapacity,
                            RpcEndpointPort =
                                snapshot.RpcEndpointPort
                        });
                }
            }
        }
        return Finalize(
            EventLogBatchQuery.ForChannels(
                queries),
            snapshot);
    }

    private static EventLogBatchQuery CreateManagedForwardedBatch(
        IReadOnlyList<string?> machines,
        EventFilter? filter,
        EventLogQueryOptions options) {

        ForwardedEventsQuerySafety.ValidateTimeWindow(
            filter?.StartTime,
            filter?.EndTime);
        Func<EventObject, bool>? predicate =
            ManagedEventFilter.CreatePredicate(filter);
        EventReadMode readMode =
            ManagedEventFilter.RequiresStructuredData(filter)
                ? UpgradeForStructuredData(options.ReadMode)
                : options.ReadMode;
        EventLogChannelQuery[] queries = machines
            .Select(machine => {
                bool local = EventLogTarget.IsLocalMachine(machine);
                var query = new EventLogChannelQuery(
                    ForwardedEventsQuerySafety.ChannelName) {
                    MachineName = local ? null : machine,
                    Credential = local ? null : options.Credential,
                    Authentication = options.Authentication,
                    XPath = "*",
                    Oldest = options.Oldest,
                    ReadMode = readMode,
                    MessageCulture = options.MessageCulture,
                    FallbackMessageCulture = options.FallbackMessageCulture,
                    IncludeBookmark = options.IncludeBookmark,
                    BookmarkXml = options.BookmarkXml,
                    BookmarkOffset = options.BookmarkOffset,
                    StrictBookmark = options.StrictBookmark,
                    RemoteConnectionTimeoutMilliseconds =
                        options.RemoteConnectionTimeoutMilliseconds,
                    RemoteReadTimeoutMilliseconds =
                        options.RemoteReadTimeoutMilliseconds,
                    BufferCapacity = options.BufferCapacity,
                    RpcEndpointPort = options.RpcEndpointPort,
                    ManagedPredicate = predicate,
                    ManagedMaxEventsScanned = options.MaxEventsScanned
                };
                ForwardedEventsQuerySafety.Apply(
                    query,
                    filter?.StartTime,
                    filter?.EndTime);
                return query;
            })
            .ToArray();
        EventLogBatchQuery batch = EventLogBatchQuery.ForChannels(queries);
        batch.MaxEvents = options.MaxEvents;
        batch.MaxConcurrency = options.MaxConcurrency;
        batch.ContinueOnError = options.ContinueOnError;
        batch.FailureHandler = options.FailureHandler;
        return batch;
    }

    private static EventReadMode UpgradeForStructuredData(
        EventReadMode readMode) {

        return readMode switch {
            EventReadMode.Metadata => EventReadMode.StructuredData,
            EventReadMode.Message => EventReadMode.StructuredDataAndMessage,
            _ => readMode
        };
    }

    /// <summary>
    /// Builds a consolidated offline-file batch with native filter partitioning.
    /// </summary>
    public static EventLogBatchQuery ForFiles(
        IEnumerable<string> paths,
        EventFilter? filter = null,
        EventLogQueryOptions? options = null) {

        string[] files = NormalizeSources(
                paths,
                nameof(paths),
                FileSystemPathIdentity.Comparer)
            .Select(Path.GetFullPath)
            .ToArray();
        EventFilter? namedDataSuppression =
            EventFilterCompiler
                .CreateExcludedNamedDataSuppression(
                    filter);
        IReadOnlyList<EventFilter> suppressionPartitions =
            EventFilterPartitioner
                .PartitionNamedDataSuppression(
                    namedDataSuppression);
        EventFilter[] filters = Partition(
            EventFilterCompiler
                .WithoutExcludedNamedData(filter));
        EventLogQueryOptions snapshot =
            SnapshotAndValidate(options);
        if (snapshot.Credential != null) {
            throw new ArgumentException(
                "Offline file queries cannot use remote credentials.",
                nameof(options));
        }
        if (namedDataSuppression != null) {
            var structured =
                new List<EventLogStructuredQuery>(
                    filters.Length);
            foreach (EventFilter partition in filters) {
                structured.Add(
                    CreateStructuredQuery(
                        EventFilterCompiler
                            .BuildFileQueryXmlWithSuppressions(
                                files,
                                partition,
                                suppressionPartitions),
                        EventLogQuerySourceKind.File,
                        machineName: null,
                        snapshot));
            }
            return Finalize(
                EventLogBatchQuery.ForStructured(
                    structured),
                snapshot);
        }
        var queries =
            new List<EventLogFileQuery>(
                checked(
                    files.Length *
                    filters.Length));
        foreach (string file in files) {
            foreach (EventFilter partition in
                     filters) {
                queries.Add(
                    new EventLogFileQuery(file) {
                        XPath =
                            EventFilterCompiler.BuildXPath(
                                partition),
                        Oldest = snapshot.Oldest,
                        ReadMode =
                            snapshot.ReadMode,
                        MessageCulture =
                            snapshot.MessageCulture,
                        FallbackMessageCulture =
                            snapshot.FallbackMessageCulture,
                        IncludeBookmark =
                            snapshot.IncludeBookmark,
                        BookmarkXml = snapshot.BookmarkXml,
                        BookmarkOffset = snapshot.BookmarkOffset,
                        StrictBookmark = snapshot.StrictBookmark
                    });
            }
        }
        return Finalize(
            EventLogBatchQuery.ForFiles(
                queries),
            snapshot);
    }

    private static EventLogStructuredQuery
        CreateStructuredQuery(
            string queryXml,
            EventLogQuerySourceKind sourceKind,
            string? machineName,
            EventLogQueryOptions options) {

        bool local =
            EventLogTarget.IsLocalMachine(
                machineName);
        return new EventLogStructuredQuery(
            queryXml) {
            SourceKind = sourceKind,
            MachineName =
                local ? null : machineName,
            Credential =
                local
                    ? null
                    : options.Credential,
            Authentication =
                options.Authentication,
            Oldest = options.Oldest,
            ReadMode = options.ReadMode,
            MessageCulture =
                options.MessageCulture,
            FallbackMessageCulture =
                options.FallbackMessageCulture,
            IncludeBookmark =
                options.IncludeBookmark,
            BookmarkXml = options.BookmarkXml,
            BookmarkOffset = options.BookmarkOffset,
            StrictBookmark = options.StrictBookmark,
            RemoteConnectionTimeoutMilliseconds =
                options.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds =
                options.RemoteReadTimeoutMilliseconds,
            BufferCapacity =
                options.BufferCapacity,
            RpcEndpointPort =
                options.RpcEndpointPort
        };
    }

    private static EventLogBatchQuery Finalize(
        EventLogBatchQuery batch,
        EventLogQueryOptions options) {

        batch.MaxEvents = options.MaxEvents;
        batch.MaxConcurrency =
            options.MaxConcurrency;
        batch.ContinueOnError =
            options.ContinueOnError;
        batch.FailureHandler =
            options.FailureHandler;
        return EventLogBatchConsolidator.Consolidate(
            batch);
    }

    private static EventFilter[] Partition(
        EventFilter? filter) {

        return filter == null
            ? new[] { new EventFilter() }
            : EventFilterPartitioner.Partition(
                    filter)
                .ToArray();
    }

    private static string[] NormalizeSources(
        IEnumerable<string> sources,
        string parameterName,
        StringComparer comparer) {

        if (sources == null) {
            throw new ArgumentNullException(
                parameterName);
        }
        string[] normalized = sources
            .Select(static source =>
                source?.Trim() ??
                string.Empty)
            .Where(static source =>
                source.Length > 0)
            .Distinct(comparer)
            .ToArray();
        if (normalized.Length == 0) {
            throw new ArgumentException(
                "At least one non-empty event source is required.",
                parameterName);
        }
        return normalized;
    }

    private static string?[] NormalizeMachines(
        IEnumerable<string?>? machineNames) {

        IEnumerable<string?> candidates =
            machineNames ??
            new string?[] { null };
        string?[] normalized = candidates
            .Select(static machine =>
                EventLogTarget.IsLocalMachine(machine)
                    ? null
                    : machine?.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0
            ? new string?[] { null }
            : normalized;
    }

    private static EventLogQueryOptions SnapshotAndValidate(
        EventLogQueryOptions? options) {

        options ??=
            new EventLogQueryOptions();
        EventReadModeValidation.EnsureDefined(
            options.ReadMode,
            nameof(options));
        if (options.MaxEvents < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum events must be greater than or equal to zero.");
        }
        if (options.MaxEventsScanned < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum scanned events must be greater than or equal to zero.");
        }
        if (options.MaxConcurrency <= 0 ||
            options.MaxConcurrency >
            EventLogLimits.MaximumConcurrency) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Maximum concurrency must be between 1 and {EventLogLimits.MaximumConcurrency}.");
        }
        if (options.RemoteConnectionTimeoutMilliseconds <=
            0) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Remote connection timeout must be greater than zero.");
        }
        if (options.RemoteReadTimeoutMilliseconds < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Remote read timeout must be greater than or equal to zero.");
        }
        if (options.BufferCapacity <= 0 ||
            options.BufferCapacity > 4096) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Buffer capacity must be between 1 and 4096.");
        }
        if (options.RpcEndpointPort <= 0 ||
            options.RpcEndpointPort > 65535) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "RPC endpoint port must be between 1 and 65535.");
        }
        if (!Enum.IsDefined(
                typeof(EventLogAuthentication),
                options.Authentication)) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The remote authentication value is not supported.");
        }
        if (string.IsNullOrWhiteSpace(options.BookmarkXml) &&
            options.BookmarkOffset != 1) {
            throw new ArgumentException(
                "BookmarkOffset requires BookmarkXml.",
                nameof(options));
        }

        return new EventLogQueryOptions {
            Oldest = options.Oldest,
            ReadMode = options.ReadMode,
            MessageCulture =
                options.MessageCulture,
            FallbackMessageCulture =
                options.FallbackMessageCulture,
            MaxEvents = options.MaxEvents,
            MaxEventsScanned = options.MaxEventsScanned,
            IncludeBookmark =
                options.IncludeBookmark,
            BookmarkXml = options.BookmarkXml,
            BookmarkOffset = options.BookmarkOffset,
            StrictBookmark = options.StrictBookmark,
            Credential =
                EventLogCredentialIdentity.Copy(
                    options.Credential),
            Authentication =
                options.Authentication,
            RemoteConnectionTimeoutMilliseconds =
                options.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds =
                options.RemoteReadTimeoutMilliseconds,
            BufferCapacity =
                options.BufferCapacity,
            RpcEndpointPort =
                options.RpcEndpointPort,
            MaxConcurrency =
                options.MaxConcurrency,
            ContinueOnError =
                options.ContinueOnError,
            FailureHandler =
                options.FailureHandler
        };
    }
}
