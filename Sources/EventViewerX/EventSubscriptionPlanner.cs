namespace EventViewerX;

/// <summary>Validates a high-level subscription and compiles it to native Windows queries.</summary>
public static class EventSubscriptionPlanner {
    /// <summary>Builds native subscription queries without PowerShell dependencies.</summary>
    public static IReadOnlyList<EventLogSubscriptionQuery> CreateQueries(
        EventSubscriptionDefinition definition,
        CancellationToken cancellationToken = default) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        string logName = definition.LogName?.Trim() ?? string.Empty;
        if (logName.Length == 0) {
            throw new ArgumentException("LogName cannot be empty.", nameof(definition));
        }
        if (definition.Filter?.HasAny == true &&
            !string.IsNullOrWhiteSpace(definition.FilterXPath)) {
            throw new ArgumentException(
                "Filter and FilterXPath cannot be combined.",
                nameof(definition));
        }
        if (EventLogTarget.IsLocalMachine(definition.MachineName) &&
            definition.Credential != null) {
            throw new ArgumentException(
                "Credential can only be used with a remote MachineName.",
                nameof(definition));
        }
        ValidateBookmark(definition);
        if (definition.BufferCapacity <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "BufferCapacity must be greater than zero.");
        }
        if (definition.RemoteConnectionTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "RemoteConnectionTimeoutMilliseconds must be greater than zero.");
        }

        string query = !string.IsNullOrWhiteSpace(definition.FilterXPath)
            ? definition.FilterXPath!.Trim()
            : CompileFilter(definition, logName, cancellationToken);
        if (query.Length == 0) {
            throw new ArgumentException("The compiled subscription query is empty.", nameof(definition));
        }
        return new[] {
            new EventLogSubscriptionQuery(logName) {
                MachineName = EventLogTarget.IsLocalMachine(definition.MachineName)
                    ? null
                    : definition.MachineName,
                Credential = definition.Credential,
                Authentication = definition.Authentication,
                XPath = query,
                Start = definition.Start,
                BookmarkXml = definition.BookmarkXml,
                StrictBookmark = definition.StrictBookmark,
                TolerateQueryErrors = definition.TolerateQueryErrors,
                ReadMode = definition.ReadMode,
                MessageCulture = definition.MessageCulture,
                FallbackMessageCulture = definition.FallbackMessageCulture,
                BufferCapacity = definition.BufferCapacity,
                RemoteConnectionTimeoutMilliseconds =
                    definition.RemoteConnectionTimeoutMilliseconds
            }
        };
    }

    private static string CompileFilter(
        EventSubscriptionDefinition definition,
        string logName,
        CancellationToken cancellationToken) {

        EventFilter filter = definition.Filter?.Clone() ?? new EventFilter();
        if (filter.ProviderNames?.Any(ContainsWildcard) == true) {
            var catalog = new EventLogCatalogQuery {
                MachineName = definition.MachineName,
                Credential = definition.Credential,
                Authentication = definition.Authentication,
                ConnectionTimeoutMilliseconds =
                    definition.RemoteConnectionTimeoutMilliseconds,
                Culture = definition.MessageCulture
            };
            string[] providers = EventLogCatalog.GetProviderNames(
                    catalog,
                    filter.ProviderNames,
                    cancellationToken)
                .ToArray();
            if (providers.Length == 0) {
                throw new ArgumentException(
                    "The subscription provider patterns did not match any registered provider.",
                    nameof(definition));
            }
            filter.ProviderNames = providers;
        }
        EventFilterCompiler.SplitNamedDataExclusions(
            filter,
            out EventFilter? select,
            out EventFilter? suppression);
        IReadOnlyList<EventFilter> partitions =
            EventFilterPartitioner.Partition(select!);
        IReadOnlyList<EventFilter> suppressions =
            EventFilterPartitioner.PartitionNamedDataSuppression(suppression);
        return partitions.Count == 1 && suppressions.Count == 0
            ? EventFilterCompiler.BuildXPath(partitions[0])
            : EventFilterCompiler.BuildChannelUnionQueryXml(
                new[] { logName },
                partitions,
                suppressions);
    }

    private static void ValidateBookmark(EventSubscriptionDefinition definition) {
        if (definition.Start == EventLogSubscriptionStart.AfterBookmark &&
            string.IsNullOrWhiteSpace(definition.BookmarkXml)) {
            throw new ArgumentException(
                "Start=AfterBookmark requires BookmarkXml.",
                nameof(definition));
        }
        if (definition.Start != EventLogSubscriptionStart.AfterBookmark &&
            !string.IsNullOrWhiteSpace(definition.BookmarkXml)) {
            throw new ArgumentException(
                "BookmarkXml requires Start=AfterBookmark.",
                nameof(definition));
        }
    }

    private static bool ContainsWildcard(string value) {
        return value?.IndexOf('*') >= 0 || value?.IndexOf('?') >= 0;
    }
}