using System.Collections;
using System.Net;

namespace PSEventViewer;

public sealed partial class CmdletGetEVXEvent {
    private void ConfigureBatch(EventLogBatchQuery batch) {
        batch.MaxEvents = GetNativeCandidateLimit();
        batch.MaxConcurrency = DisableParallel.IsPresent
            ? 1
            : MaxConcurrency;
        batch.ContinueOnError = ContinueOnError;
        batch.FailureHandler = failure => WriteError(new ErrorRecord(
            failure.Exception,
            "EVXEventQuerySourceFailed",
            ErrorCategory.ReadError,
            string.IsNullOrWhiteSpace(failure.MachineName)
                ? failure.Source
                : $"{failure.Source} on {failure.MachineName}"));
    }

    private EventFilter CreateCommandFilter() {
        EventFilter? nativeFilter = ResolveNativeFilter();
        if (nativeFilter != null) {
            string[] conflicting = new[] {
                    nameof(EventId),
                    nameof(EventRecordId),
                    nameof(Keywords),
                    nameof(Level),
                    nameof(StartTime),
                    nameof(EndTime),
                    nameof(TimePeriod),
                    nameof(UserId),
                    nameof(NamedDataFilter),
                    nameof(NamedDataExcludeFilter)
                }
                .Where(MyInvocation.BoundParameters.ContainsKey)
                .ToArray();
            if (conflicting.Length > 0) {
                throw new PSArgumentException(
                    "Filter cannot be combined with individual filter parameters: " +
                    string.Join(", ", conflicting) + ".");
            }
            if (ParameterSetName == "Provider" &&
                (nativeFilter.ProviderNames?.Count ?? 0) > 0) {
                throw new PSArgumentException(
                    "ProviderName already defines the provider source in the Provider parameter set; Filter.ProviderNames must be empty.");
            }
            return nativeFilter;
        }
        (DateTime? resolvedStart, DateTime? resolvedEnd) =
            EventTimeRange.Resolve(StartTime, EndTime, TimePeriod);
        return new EventFilter {
            EventIds = EventId,
            RecordIds = EventRecordId,
            ProviderNames = ProviderName,
            Levels = Level?.Select(static value => (byte)value).ToArray(),
            Keywords = Keywords,
            StartTime = resolvedStart,
            EndTime = resolvedEnd,
            UserIds = UserId,
            NamedData = ConvertNamedData(NamedDataFilter),
            ExcludedNamedData = ConvertNamedData(NamedDataExcludeFilter)
        };
    }

    private EventFilter? ResolveNativeFilter() {
        object? value = Filter;
        while (value is PSObject wrapper && wrapper.BaseObject != value) {
            value = wrapper.BaseObject;
        }
        if (value == null) {
            return null;
        }
        return value as EventFilter ?? throw new PSArgumentException(
            "Native Channel, Path, and Provider queries require an EventFilter from New-EVXFilter without Type or Definition.",
            nameof(Filter));
    }

    private EventFilter CopyFilterWithCheckpoint(
        EventFilter source,
        string? machineName,
        string sourceName,
        bool sourceIsFile = false) {

        long? checkpointMinimum = GetCheckpointLowerBound(
            machineName,
            sourceName,
            sourceIsFile);
        return source.WithMinimumRecordIdExclusive(
            checkpointMinimum);
    }

    private static EventFilter WithProviders(
        EventFilter source,
        IReadOnlyList<string> providerNames) {

        EventFilter copy = source.Clone();
        copy.ProviderNames = providerNames.ToArray();
        return copy;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>?
        ConvertNamedData(Hashtable? table) {

        if (table == null || table.Count == 0) {
            return null;
        }
        var converted = new Dictionary<string, IReadOnlyList<string>>(
            StringComparer.Ordinal);
        foreach (DictionaryEntry entry in table) {
            string key = ConvertNamedDataValue(entry.Key);
            if (key.Length == 0) {
                throw new PSArgumentException(
                    "Named-data filter keys cannot be empty.");
            }
            IEnumerable values =
                entry.Value is string || entry.Value is not IEnumerable enumerable
                    ? new[] { entry.Value }
                    : enumerable;
            converted[key] = values
                .Cast<object?>()
                .Select(static value => ConvertNamedDataValue(value))
                .ToArray();
        }
        return converted;
    }

    private static string ConvertNamedDataValue(object? value) {
        if (value is PSObject psObject) {
            value = psObject.BaseObject;
        }
        return EventFilterValueConverter.ToInvariantString(value);
    }

    private static string[] NormalizeRequiredValues(
        IEnumerable<string> values,
        string parameterName,
        StringComparer? comparer = null) {

        string[] normalized = values
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => value.Length > 0)
            .Distinct(comparer ?? StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length == 0) {
            throw new PSArgumentException(
                $"Parameter '{parameterName}' requires at least one non-empty value.");
        }
        return normalized;
    }

    private IReadOnlyList<string> ExpandFilePaths(
        IEnumerable<string> values,
        string parameterName) {

        var paths = new HashSet<string>(FileSystemPathIdentity.Comparer);
        string[] requestedPaths = FileSystemPathIdentity.NormalizeUnresolvedPaths(values);
        if (requestedPaths.Length == 0) {
            throw new PSArgumentException(
                $"Parameter '{parameterName}' requires at least one non-empty value.");
        }
        foreach (string value in requestedPaths) {
            string pathValue = FileSystemPathIdentity
                .NormalizeWindowsExtendedLengthPath(
                    value.Trim().Trim('"', '\''));
            string wildcardCandidate =
                FileSystemPathIdentity.GetWildcardCandidate(pathValue);
            if (!WildcardPattern.ContainsWildcardCharacters(wildcardCandidate)) {
                paths.Add(FileSystemPathIdentity.GetFullPath(
                    pathValue));
                continue;
            }
            ProviderInfo provider;
            foreach (string resolved in
                     SessionState.Path.GetResolvedProviderPathFromPSPath(
                         pathValue,
                         out provider)) {
                if (!string.Equals(
                        provider.Name,
                        "FileSystem",
                        StringComparison.OrdinalIgnoreCase)) {
                    throw new PSArgumentException(
                        $"Path pattern '{value}' resolved through provider '{provider.Name}', but event log paths must use FileSystem.");
                }
                paths.Add(FileSystemPathIdentity.GetFullPath(resolved));
            }
        }
        if (paths.Count == 0) {
            throw new ItemNotFoundException(
                $"No event log files matched parameter '{parameterName}'.");
        }
        return paths
            .OrderBy(static path => path, FileSystemPathIdentity.Comparer)
            .ToArray();
    }

    private IReadOnlyList<string> ExpandChannelPatterns(
        IReadOnlyList<string> values,
        string? machineName) {

        if (!values.Any(WildcardPattern.ContainsWildcardCharacters)) {
            return values;
        }
        var query = new EventLogCatalogQuery {
            MachineName = machineName,
            Credential = Credential?.GetNetworkCredential(),
            Authentication = Authentication,
            ConnectionTimeoutMilliseconds =
                EffectiveRemoteConnectionTimeoutMilliseconds,
            Culture = MessageCulture
        };
        IReadOnlyList<string> channels =
            EventLogCatalog.GetChannelNames(
                query,
                values,
                includeAnalyticDebug: Force.IsPresent,
                cancellationToken: CancelToken);
        if (channels.Count == 0) {
            throw new ItemNotFoundException(
                $"No event channels match pattern(s) '{string.Join(", ", values)}' on '{machineName ?? Environment.MachineName}'.");
        }
        return channels;
    }

    private void ValidateRawXPathCombination(
        string? rawXPath,
        EventFilter filter) {

        if (string.IsNullOrWhiteSpace(rawXPath)) {
            return;
        }
        if (filter.HasAny) {
            throw new PSArgumentException(
                "FilterXPath cannot be combined with EventId, EventRecordId, ProviderName, Keywords, Level, StartTime, EndTime, TimePeriod, UserId, or named-data filters.");
        }
        if (UsesCheckpoint) {
            throw new PSArgumentException(
                "FilterXPath cannot be combined with RecordIdFile because an opaque XPath cannot be safely rewritten. Use BookmarkXml or include the record boundary in FilterXPath.");
        }
    }

    private void ValidateRemoteCredentialTargets(
        IReadOnlyList<string?> machines) {

        if (Credential == null) {
            return;
        }
        if (machines.Any(static machine =>
                EventLogTarget.IsLocalMachine(machine))) {
            throw new PSArgumentException(
                "Credential can only be used when every MachineName is remote.");
        }
    }

    private void ValidateBookmarkFanOut(int sourceCount) {
        if (string.IsNullOrWhiteSpace(BookmarkXml)) {
            if (MyInvocation.BoundParameters.ContainsKey(
                    nameof(BookmarkOffset)) ||
                IgnoreStaleBookmark) {
                throw new PSArgumentException(
                    "BookmarkOffset and IgnoreStaleBookmark require BookmarkXml.");
            }
            return;
        }
        if (UsesCheckpoint) {
            throw new PSArgumentException(
                "BookmarkXml and RecordIdFile are separate resume models and cannot be combined.");
        }
        if (sourceCount != 1) {
            throw new PSArgumentException(
                "BookmarkXml can target exactly one independent query source.");
        }
    }

    private EventLogBatchQuery ConsolidateAndValidateBookmarkFanOut(
        EventLogBatchQuery batch) {

        EventLogBatchQuery consolidated =
            EventLogBatchConsolidator.Consolidate(batch);
        int sourceCount = checked(
            consolidated.ChannelQueries.Count +
            consolidated.FileQueries.Count +
            consolidated.StructuredQueries.Sum(
                static query =>
                    query.GetIndependentSourceCount()));
        ValidateBookmarkFanOut(sourceCount);
        return consolidated;
    }

    private long GetNativeCandidateLimit() {
        if (MaxEventsScanned > 0) {
            return MaxEventsScanned;
        }
        return HasManagedPostReadFilter ? 0 : MaxEvents;
    }
}
