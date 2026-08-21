using System.Collections;
using System.Net;

namespace PSEventViewer;

public sealed partial class CmdletGetEVXEvent {
    private IReadOnlyList<CheckpointSource> GetCheckpointSources() {
        return _checkpointSources ??=
            ResolveCheckpointSources();
    }

    private IReadOnlyList<CheckpointSource> ResolveCheckpointSources() {
        switch (ParameterSetName) {
            case "Path":
                return ExpandFilePaths(Path, nameof(Path))
                    .Select(static path =>
                        new CheckpointSource(path, isFile: true))
                    .ToArray();
            case "Hashtable":
                Hashtable[] hashtables =
                    GetFilterHashtables();
                PowerShellEventFilterBinding[] bindings =
                    hashtables
                        .Select(PowerShellEventFilterAdapter.Bind)
                        .ToArray();
                bool hasChannels = bindings.Any(
                    static binding =>
                        binding.UsesChannels ||
                        binding.ProviderOnly);
                bool hasFiles = bindings.Any(
                    static binding => binding.UsesFiles);
                if (UsesCheckpoint &&
                    (bindings.Length != 1 ||
                     (hasChannels && hasFiles))) {
                    throw new PSArgumentException(
                        "RecordIdFile requires one FilterHashtable targeting only channels or only files. Multiple or mixed sources can have unrelated monotonic record sequences.");
                }
                var filterSources = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (PowerShellEventFilterBinding binding in
                         bindings) {
                    IEnumerable<string> sources;
                    if (binding.ProviderOnly) {
                        sources = ResolveCheckpointProviderChannels(
                            binding.Select.ProviderNames ??
                            Array.Empty<string>());
                    } else {
                        sources = binding.UsesFiles
                            ? ExpandFilePaths(
                                binding.Paths,
                                nameof(FilterHashtable))
                            : Array.Empty<string>();
                        foreach (string channel in
                                 ExpandCheckpointChannels(
                                     binding.LogNames)) {
                            filterSources.Add(channel);
                        }
                    }
                    foreach (string source in sources) {
                        filterSources.Add(source);
                    }
                }
                bool filesOnly = hasFiles && !hasChannels;
                return filterSources
                    .OrderBy(
                        static source => source,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(source =>
                        new CheckpointSource(source, filesOnly))
                    .ToArray();
            case "Xml":
                EventLogStructuredQuerySource[] structuredSources =
                    new EventLogStructuredQuery(
                            FilterXml!.OuterXml)
                        .ResolveSources()
                        .ToArray();
                if (structuredSources.Length == 0) {
                    throw new PSArgumentException(
                        "RecordIdFile requires FilterXml to declare at least one channel or offline-file Path.");
                }
                return structuredSources
                    .Select(static source =>
                        new CheckpointSource(
                            source.Source,
                            source.Kind ==
                            EventLogQuerySourceKind.File))
                    .ToArray();
            case "TypedFilter":
                if (Collector != null) {
                    return new[] {
                        new CheckpointSource(
                            "ForwardedEvents",
                            isFile: false)
                    };
                }
                if (_typedFilter?.Type != null) {
                    return EventTypeCatalog
                        .GetSourceMap(Type)
                        .Keys
                        .Select(static source =>
                            new CheckpointSource(source, isFile: false))
                        .ToArray();
                }
                if (_typedFilter?.Definition != null) {
                    return ResolveEventDefinition().Sources
                        .Select(static source => source.LogName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(static source =>
                            new CheckpointSource(source, isFile: false))
                        .ToArray();
                }
                throw new PSArgumentException(
                    "Typed Filter checkpoint sources require a retained Type or Definition.",
                    nameof(Filter));
            case "Type":
                if (Path.Length > 0) {
                    return ExpandFilePaths(Path, nameof(Path))
                        .Select(static path => new CheckpointSource(path, isFile: true))
                        .ToArray();
                }
                if (Collector != null) {
                    return new[] {
                        new CheckpointSource(
                            "ForwardedEvents",
                            isFile: false)
                    };
                }
                IReadOnlyList<string> namedSources = EventTypeCatalog
                    .GetSourceMap(Type)
                    .Keys
                    .ToArray();
                return namedSources
                    .Select(static source =>
                        new CheckpointSource(source, isFile: false))
                    .ToArray();
            case "Definition":
                if (Path.Length > 0) {
                    return ExpandFilePaths(Path, nameof(Path))
                        .Select(static path => new CheckpointSource(path, isFile: true))
                        .ToArray();
                }
                if (Collector != null) {
                    return new[] { new CheckpointSource("ForwardedEvents", isFile: false) };
                }
                return ResolveEventDefinition().Sources
                    .Select(static source => source.LogName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(static source => new CheckpointSource(source, isFile: false))
                    .ToArray();
            case "Channel":
                return ExpandCheckpointChannels(
                        NormalizeRequiredValues(
                            LogName,
                            nameof(LogName)))
                    .Select(static source =>
                        new CheckpointSource(source, isFile: false))
                    .ToArray();
            case "Provider":
                return ResolveCheckpointProviderChannels(
                        ProviderName ?? Array.Empty<string>())
                    .Select(static source =>
                        new CheckpointSource(source, isFile: false))
                    .ToArray();
            default:
                return Array.Empty<CheckpointSource>();
        }
    }

    private int GetCheckpointSourceCount() {
        return GetCheckpointSources().Count;
    }

    private IReadOnlyList<string> ResolveCheckpointProviderChannels(
        IEnumerable<string> providerPatterns) {

        var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? machine in
                 EventLogTarget.NormalizeMachineNames(MachineName)) {
            var catalogQuery = new EventLogCatalogQuery {
                MachineName = machine,
                Credential = Credential?.GetNetworkCredential(),
                Authentication = Authentication,
                ConnectionTimeoutMilliseconds =
                    EffectiveRemoteConnectionTimeoutMilliseconds,
                Culture = MessageCulture
            };
            foreach (string channel in
                     EventLogCatalog.ResolveProviderChannels(
                         catalogQuery,
                         providerPatterns,
                         CancelToken)) {
                channels.Add(channel);
            }
        }
        return channels
            .OrderBy(static channel => channel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> ExpandCheckpointChannels(
        IReadOnlyList<string> logNames) {

        var channels = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string? machine in
                 EventLogTarget.NormalizeMachineNames(MachineName)) {
            foreach (string channel in
                     ExpandChannelPatterns(logNames, machine)) {
                channels.Add(channel);
            }
        }
        return channels
            .OrderBy(static channel => channel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private readonly struct CheckpointSource {
        internal CheckpointSource(
            string name,
            bool isFile) {

            Name = name;
            IsFile = isFile;
        }

        internal string Name { get; }

        internal bool IsFile { get; }
    }

}
