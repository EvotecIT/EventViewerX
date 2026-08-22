using System.Diagnostics.Eventing.Reader;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Channel policy getters over the supported Windows Event Log channel API.
/// </summary>
public static partial class EventLogChannelPolicyService {
    /// <summary>
    /// Returns a channel policy for a single log.
    /// </summary>
    public static ChannelPolicy? Get(string logName, string? machineName = null) {
        return Get(
            logName,
            new EventLogCatalogQuery {
                MachineName = machineName
            });
    }

    /// <summary>
    /// Returns a channel policy using bounded local or remote catalog options.
    /// </summary>
    public static ChannelPolicy? Get(
        string logName,
        EventLogCatalogQuery query) => Get(
            logName,
            query,
            CancellationToken.None);

    internal static ChannelPolicy? Get(
        string logName,
        EventLogCatalogQuery query,
        CancellationToken cancellationToken) {

        if (string.IsNullOrWhiteSpace(logName)) {
            throw new ArgumentException("logName cannot be null or empty", nameof(logName));
        }
        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        query = EventLogCatalog.SnapshotAndValidate(
            query);

        EventLogSession session =
            EventLogSessionManager.OpenRequiredSession(
                query.MachineName,
                "ChannelPolicy.Get",
                logName,
                query.ConnectionTimeoutMilliseconds,
                query.Credential,
                query.Authentication,
                cancellationToken);
        using var sessionLifetime =
            new RetainedDisposable<EventLogSession>(
                session);
        string target = EventLogTarget.IsLocalMachine(
                query.MachineName)
            ? EventLogTarget.LocalMachineName
            : query.MachineName!;
        return EventLogNativeOperation.Execute(
            () => {
                using var configuration =
                    new EventLogConfiguration(
                        logName,
                        sessionLifetime.Value);
                return CreateSnapshot(
                    configuration,
                    query.MachineName,
                    query.Credential,
                    query.Authentication,
                    query.ConnectionTimeoutMilliseconds);
            },
            query.ConnectionTimeoutMilliseconds,
            $"Timed out reading channel policy for '{logName}' on '{target}' after {query.ConnectionTimeoutMilliseconds} ms.",
            cancellationToken,
            operationLease:
                sessionLifetime.Retain());
    }

    /// <summary>
    /// Enumerates policies for all logs on a machine.
    /// </summary>
    /// <param name="machineName">Machine name or null for local.</param>
    /// <param name="includePatterns">Optional wildcard filters.</param>
    /// <param name="parallel">If true, enumerate policies in parallel. Defaults to false.</param>
    /// <param name="degreeOfParallelism">When parallel, max concurrency. Defaults to Environment.ProcessorCount.</param>
    public static IEnumerable<ChannelPolicy> GetMany(string? machineName = null, string[]? includePatterns = null, bool parallel = false, int? degreeOfParallelism = null) {
        return GetMany(
            new EventLogCatalogQuery {
                MachineName = machineName
            },
            includePatterns,
            parallel,
            degreeOfParallelism);
    }

    /// <summary>
    /// Enumerates policies using bounded remote authentication and catalog
    /// options.
    /// </summary>
    public static IEnumerable<ChannelPolicy> GetMany(
        EventLogCatalogQuery query,
        string[]? includePatterns = null,
        bool parallel = false,
        int? degreeOfParallelism = null) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventLogCatalogQuery snapshot =
            EventLogCatalog.SnapshotAndValidate(
                query);
        string[] patternSnapshot =
            includePatterns?.ToArray() ??
            Array.Empty<string>();
        int dop = Math.Max(
            1,
            degreeOfParallelism ??
            Environment.ProcessorCount);
        if (parallel &&
            dop > EventLogLimits.MaximumConcurrency) {
            throw new ArgumentOutOfRangeException(
                nameof(degreeOfParallelism),
                $"Maximum degree of parallelism cannot exceed {EventLogLimits.MaximumConcurrency}.");
        }
        return GetManyIterator(
            snapshot,
            patternSnapshot,
            parallel,
            dop);
    }

    private static IEnumerable<ChannelPolicy> GetManyIterator(
        EventLogCatalogQuery query,
        string[] includePatterns,
        bool parallel,
        int degreeOfParallelism) {

        string[] names = EventLogCatalog.GetChannelNames(
                query,
                includePatterns)
            .ToArray();
        IEnumerable<string> filtered = names;

        if (parallel) {
            var policies = filtered
                .AsParallel()
                .WithDegreeOfParallelism(
                    degreeOfParallelism)
                .Select(name => Get(name, query))
                .Where(static policy => policy != null)
                .Cast<ChannelPolicy>();
            foreach (ChannelPolicy policy in policies) {
                yield return policy;
            }
        } else {
            foreach (string name in filtered) {
                ChannelPolicy? policy =
                    Get(name, query);
                if (policy != null) {
                    yield return policy;
                }
            }
        }
    }

    private static ChannelPolicy CreateSnapshot(
        EventLogConfiguration configuration,
        string? machineName,
        System.Net.NetworkCredential? credential,
        EventLogAuthentication authentication,
        int connectionTimeoutMilliseconds) {

        return new ChannelPolicy {
            LogName = configuration.LogName,
            MachineName = machineName,
            Credential = credential,
            Authentication = authentication,
            ConnectionTimeoutMilliseconds =
                connectionTimeoutMilliseconds,
            IsEnabled = configuration.IsEnabled,
            MaximumSizeInBytes =
                configuration.MaximumSizeInBytes,
            LogFilePath = configuration.LogFilePath,
            Isolation = configuration.LogIsolation,
            Mode = configuration.LogMode,
            SecurityDescriptor =
                configuration.SecurityDescriptor
        };
    }
}
