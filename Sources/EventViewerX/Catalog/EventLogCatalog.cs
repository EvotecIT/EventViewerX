using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>Reusable channel and provider catalog service.</summary>
public static partial class EventLogCatalog {
    /// <summary>Enumerates provider names with wildcard filtering without opening provider metadata.</summary>
    public static IReadOnlyList<string> GetProviderNames(
        EventLogCatalogQuery? query = null,
        IEnumerable<string>? providerPatterns = null,
        CancellationToken cancellationToken = default) {

        cancellationToken.ThrowIfCancellationRequested();
        EventLogCatalogQuery snapshot = SnapshotAndValidate(query);
        Regex[] patterns = CompilePatterns(providerPatterns);
        using var sessionLifetime =
            new RetainedDisposable<EventLogSession>(
                OpenSession(
                    snapshot,
                    cancellationToken));
        EventLogSession session =
            sessionLifetime.Value;
        var providers = new List<string>();
        foreach (string name in EnumerateNamesBounded(
                     () => session.GetProviderNames(),
                     snapshot.ConnectionTimeoutMilliseconds,
                     $"Timed out enumerating event providers after {snapshot.ConnectionTimeoutMilliseconds} ms.",
                     cancellationToken,
                     sessionLifetime.Retain())) {
            cancellationToken.ThrowIfCancellationRequested();
            if (MatchesAny(name, patterns)) {
                providers.Add(name);
            }
        }
        providers.Sort(StringComparer.OrdinalIgnoreCase);
        return providers;
    }

    /// <summary>Enumerates detached provider metadata, preserving failures per provider.</summary>
    public static IEnumerable<EventProviderCatalogResult> GetProviders(
        EventLogCatalogQuery? query = null,
        IEnumerable<string>? providerPatterns = null,
        CancellationToken cancellationToken = default) {

        cancellationToken.ThrowIfCancellationRequested();
        EventLogCatalogQuery snapshot = SnapshotAndValidate(query);
        Regex[] patterns = CompilePatterns(providerPatterns);
        return EnumerateProviders(
            snapshot,
            patterns,
            cancellationToken);
    }

    private static IEnumerable<EventProviderCatalogResult>
        EnumerateProviders(
            EventLogCatalogQuery snapshot,
            Regex[] patterns,
            CancellationToken cancellationToken) {

        using var sessionLifetime =
            new RetainedDisposable<EventLogSession>(
                OpenSession(
                    snapshot,
                    cancellationToken));
        EventLogSession session =
            sessionLifetime.Value;
        foreach (string providerName in EnumerateNamesBounded(
                     () => session.GetProviderNames(),
                     snapshot.ConnectionTimeoutMilliseconds,
                     $"Timed out enumerating event providers after {snapshot.ConnectionTimeoutMilliseconds} ms.",
                     cancellationToken,
                     sessionLifetime.Retain())
                     .Where(name => MatchesAny(name, patterns))
                     .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)) {
            cancellationToken.ThrowIfCancellationRequested();
            EventProviderMetadataSnapshot? provider = null;
            Exception? failure = null;
            try {
                provider = SnapshotProviderBounded(
                    () => {
                        using var metadata = new ProviderMetadata(
                            providerName,
                            session,
                            snapshot.Culture ??
                            CultureInfo.CurrentUICulture);
                        return SnapshotProvider(
                            metadata,
                            snapshot.IncludeEvents);
                    },
                    providerName,
                    snapshot.ConnectionTimeoutMilliseconds,
                    cancellationToken,
                    sessionLifetime.Retain());
            } catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception exception) {
                failure = exception;
            }
            yield return new EventProviderCatalogResult(
                providerName,
                provider,
                failure);
        }
    }

    internal static EventProviderMetadataSnapshot
        SnapshotProviderBounded(
            Func<EventProviderMetadataSnapshot> snapshot,
            string providerName,
            int timeoutMilliseconds,
            CancellationToken cancellationToken,
            IDisposable? operationLease = null) {

        if (snapshot == null) {
            operationLease?.Dispose();
            throw new ArgumentNullException(
                nameof(snapshot));
        }
        if (cancellationToken.IsCancellationRequested) {
            operationLease?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        return EventLogNativeOperation.Execute(
            snapshot,
            timeoutMilliseconds,
            $"Timed out reading metadata for event provider '{providerName}' after {timeoutMilliseconds} ms.",
            cancellationToken,
            operationLease:
                operationLease);
    }

    /// <summary>Resolves the channels linked by matching providers.</summary>
    public static IReadOnlyList<string> ResolveProviderChannels(
        EventLogCatalogQuery? query,
        IEnumerable<string> providerPatterns,
        CancellationToken cancellationToken = default) {

        var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EventProviderCatalogResult result in GetProviders(
                     query,
                     providerPatterns,
                     cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.Success) {
                continue;
            }
            foreach (EventProviderLogLink link in result.Provider!.LogLinks) {
                if (!string.IsNullOrWhiteSpace(link.LogName)) {
                    channels.Add(link.LogName);
                }
            }
        }
        return channels
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Enumerates channel names with wildcard filtering.</summary>
    public static IReadOnlyList<string> GetChannelNames(
        EventLogCatalogQuery? query = null,
        IEnumerable<string>? channelPatterns = null,
        bool includeAnalyticDebug = true,
        CancellationToken cancellationToken = default) {

        cancellationToken.ThrowIfCancellationRequested();
        EventLogCatalogQuery snapshot = SnapshotAndValidate(query);
        string[] normalizedPatterns =
            NormalizePatterns(channelPatterns);
        Regex[] patterns = CompilePatterns(normalizedPatterns);
        var explicitNames = new HashSet<string>(
            normalizedPatterns.Where(static pattern =>
                !ContainsWildcard(pattern)),
            StringComparer.OrdinalIgnoreCase);
        using var sessionLifetime =
            new RetainedDisposable<EventLogSession>(
                OpenSession(
                    snapshot,
                    cancellationToken));
        EventLogSession session =
            sessionLifetime.Value;
        string[] logNames = EnumerateNamesBounded(
            () => session.GetLogNames(),
            snapshot.ConnectionTimeoutMilliseconds,
            $"Timed out enumerating event logs after {snapshot.ConnectionTimeoutMilliseconds} ms.",
            cancellationToken,
            sessionLifetime.Retain());
        var channels = new List<string>();
        foreach (string name in logNames) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MatchesAny(name, patterns) ||
                (!includeAnalyticDebug &&
                 !explicitNames.Contains(name) &&
                 IsAnalyticOrDebug(
                     session,
                     name,
                     snapshot.ConnectionTimeoutMilliseconds,
                     cancellationToken,
                     sessionLifetime.Retain()))) {
                continue;
            }
            channels.Add(name);
        }
        channels.Sort(StringComparer.OrdinalIgnoreCase);
        return channels;
    }

    private static bool IsAnalyticOrDebug(
        EventLogSession session,
        string logName,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        IDisposable? operationLease = null) {

        if (cancellationToken.IsCancellationRequested) {
            operationLease?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        try {
            bool result = EventLogNativeOperation.Execute(
                () => {
                    using var configuration =
                        new EventLogConfiguration(
                            logName,
                            session);
                    return configuration.LogType ==
                               EventLogType.Analytical ||
                           configuration.LogType ==
                               EventLogType.Debug;
                },
                timeoutMilliseconds,
                $"Timed out reading the type of event log '{logName}' after {timeoutMilliseconds} ms.",
                cancellationToken,
                operationLease:
                    operationLease);
            return result;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (EventLogException) {
            return false;
        }
    }

    internal static string[] EnumerateNamesBounded(
        Func<IEnumerable<string>> enumerate,
        int timeoutMilliseconds,
        string timeoutMessage,
        CancellationToken cancellationToken,
        IDisposable? operationLease = null) {

        if (enumerate == null) {
            operationLease?.Dispose();
            throw new ArgumentNullException(
                nameof(enumerate));
        }
        if (cancellationToken.IsCancellationRequested) {
            operationLease?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        string[] names =
            EventLogNativeOperation.Execute(
                () => enumerate().ToArray(),
                timeoutMilliseconds,
                timeoutMessage,
                cancellationToken,
                operationLease:
                    operationLease);
        return names;
    }

    private static EventProviderMetadataSnapshot SnapshotProvider(
        ProviderMetadata metadata,
        bool includeEvents) {

        var diagnostics = new List<string>();
        string displayName = Read(
            () => metadata.DisplayName,
            string.Empty,
            diagnostics,
            nameof(metadata.DisplayName));
        string messageFilePath = Read(
            () => metadata.MessageFilePath,
            string.Empty,
            diagnostics,
            nameof(metadata.MessageFilePath));
        string resourceFilePath = Read(
            () => metadata.ResourceFilePath,
            string.Empty,
            diagnostics,
            nameof(metadata.ResourceFilePath));
        string parameterFilePath = Read(
            () => metadata.ParameterFilePath,
            string.Empty,
            diagnostics,
            nameof(metadata.ParameterFilePath));
        Uri? helpLink = Read(
            () => metadata.HelpLink,
            default(Uri),
            diagnostics,
            nameof(metadata.HelpLink));
        EventProviderLogLink[] logLinks = Read(
            () => metadata.LogLinks.Select(link =>
                new EventProviderLogLink(
                    link.LogName ?? string.Empty,
                    SafeString(() => link.DisplayName),
                    link.IsImported)).ToArray(),
            Array.Empty<EventProviderLogLink>(),
            diagnostics,
            nameof(metadata.LogLinks));
        EventProviderValue[] levels = Read(
            () => metadata.Levels.Select(level =>
                new EventProviderValue(
                    level.Name ?? string.Empty,
                    SafeString(() => level.DisplayName),
                    level.Value)).ToArray(),
            Array.Empty<EventProviderValue>(),
            diagnostics,
            nameof(metadata.Levels));
        EventProviderValue[] tasks = Read(
            () => metadata.Tasks.Select(task =>
                new EventProviderValue(
                    task.Name ?? string.Empty,
                    SafeString(() => task.DisplayName),
                    task.Value,
                    task.EventGuid)).ToArray(),
            Array.Empty<EventProviderValue>(),
            diagnostics,
            nameof(metadata.Tasks));
        EventProviderValue[] opcodes = Read(
            () => metadata.Opcodes.Select(opcode =>
                new EventProviderValue(
                    opcode.Name ?? string.Empty,
                    SafeString(() => opcode.DisplayName),
                    opcode.Value)).ToArray(),
            Array.Empty<EventProviderValue>(),
            diagnostics,
            nameof(metadata.Opcodes));
        EventProviderValue[] keywords = Read(
            () => metadata.Keywords.Select(keyword =>
                new EventProviderValue(
                    keyword.Name ?? string.Empty,
                    SafeString(() => keyword.DisplayName),
                    keyword.Value)).ToArray(),
            Array.Empty<EventProviderValue>(),
            diagnostics,
            nameof(metadata.Keywords));
        EventProviderEventMetadataSnapshot[] events = includeEvents
            ? Read(
                () => metadata.Events.Select(eventMetadata =>
                    new EventProviderEventMetadataSnapshot(
                        eventMetadata.Id,
                        eventMetadata.Version,
                        eventMetadata.LogLink?.LogName ?? string.Empty,
                        null,
                        eventMetadata.Level?.Value,
                        eventMetadata.Opcode?.Value,
                        eventMetadata.Task?.Value,
                        eventMetadata.Keywords?
                            .Select(static keyword => keyword.Value)
                            .ToArray() ?? Array.Empty<long>(),
                        eventMetadata.Template ?? string.Empty,
                        SafeString(() => eventMetadata.Description)))
                    .ToArray(),
                Array.Empty<EventProviderEventMetadataSnapshot>(),
                diagnostics,
                nameof(metadata.Events))
            : Array.Empty<EventProviderEventMetadataSnapshot>();

        return new EventProviderMetadataSnapshot(
            metadata.Name,
            metadata.Id,
            displayName,
            messageFilePath,
            resourceFilePath,
            parameterFilePath,
            helpLink,
            logLinks,
            levels,
            tasks,
            opcodes,
            keywords,
            events,
            diagnostics);
    }

    private static EventLogSession OpenSession(
        EventLogCatalogQuery query,
        CancellationToken cancellationToken) {

        return EventLogSessionManager.OpenRequiredSession(
            query.MachineName,
            "Catalog",
            logName: null,
            query.ConnectionTimeoutMilliseconds,
            query.Credential,
            query.Authentication,
            cancellationToken);
    }

    internal static EventLogCatalogQuery SnapshotAndValidate(
        EventLogCatalogQuery? query) {

        query ??= new EventLogCatalogQuery();
        if (!Enum.IsDefined(
                typeof(EventLogAuthentication),
                query.Authentication)) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The catalog authentication value is not supported.");
        }
        if (query.ConnectionTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Catalog connection timeout must be greater than zero.");
        }
        string machineName = query.MachineName?.Trim() ?? string.Empty;
        if (EventLogTarget.IsLocalMachine(machineName) &&
            query.Credential != null) {
            throw new ArgumentException(
                "Credentials can only be used with a remote catalog query.",
                nameof(query));
        }
        if (!EventLogTarget.IsLocalMachine(machineName) &&
            query.Credential == null &&
            query.Authentication !=
            EventLogAuthentication.Default) {
            throw new ArgumentException(
                "An explicit catalog authentication package requires a credential because the managed Windows catalog API cannot enforce an authentication package with its current-identity overload.",
                nameof(query));
        }
        return new EventLogCatalogQuery {
            MachineName = string.IsNullOrWhiteSpace(machineName)
                ? null
                : machineName,
            Credential =
                EventLogCredentialIdentity.Copy(
                    query.Credential),
            Authentication = query.Authentication,
            ConnectionTimeoutMilliseconds =
                query.ConnectionTimeoutMilliseconds,
            Culture = query.Culture == null
                ? null
                : CultureInfo.GetCultureInfo(
                    query.Culture.Name),
            IncludeEvents = query.IncludeEvents
        };
    }

    private static Regex[] CompilePatterns(
        IEnumerable<string>? patterns) {

        string[] normalized =
            NormalizePatterns(patterns);
        return normalized
            .Select(static pattern =>
                new Regex(
                    "^" + Regex.Escape(pattern)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
            .ToArray();
    }

    private static string[] NormalizePatterns(
        IEnumerable<string>? patterns) {

        string[] normalized = patterns?
            .Select(static pattern => pattern?.Trim() ?? string.Empty)
            .Where(static pattern => pattern.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
        if (normalized.Length == 0) {
            normalized = new[] { "*" };
        }
        return normalized;
    }

    private static bool ContainsWildcard(
        string value) {

        return value.IndexOf('*') >= 0 ||
               value.IndexOf('?') >= 0;
    }

    private static bool MatchesAny(
        string value,
        IEnumerable<Regex> patterns) {

        foreach (Regex pattern in patterns) {
            if (pattern.IsMatch(value)) {
                return true;
            }
        }
        return false;
    }

    private static T Read<T>(
        Func<T> read,
        T fallback,
        ICollection<string> diagnostics,
        string propertyName) {

        try {
            return read();
        } catch (Exception exception) {
            diagnostics.Add(
                $"{propertyName}: {exception.GetType().Name}: {exception.Message}");
            return fallback;
        }
    }

    private static string SafeString(Func<string?> read) {
        try {
            return read() ?? string.Empty;
        } catch {
            return string.Empty;
        }
    }
}
