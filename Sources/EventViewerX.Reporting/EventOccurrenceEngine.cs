using System.Globalization;
using System.Security.Cryptography;

namespace EventViewerX.Reporting;

/// <summary>Creates non-destructive transport and semantic occurrence groups.</summary>
public static class EventOccurrenceEngine {
    private static readonly IEventOccurrencePolicy[] Policies = {
        new CausalIdentifierOccurrencePolicy()
    };

    /// <summary>Returns the compiled semantic occurrence policies.</summary>
    public static IReadOnlyList<IEventOccurrencePolicy> GetPolicies() => Policies.ToArray();

    /// <summary>Groups source observations without removing or rewriting any observation.</summary>
    public static EventOccurrenceResult Group(
        IEnumerable<EventReportRow> observations,
        EventOccurrenceOptions? options = null) {

        if (observations == null) {
            throw new ArgumentNullException(nameof(observations));
        }
        options ??= new EventOccurrenceOptions();
        Validate(options);
        EventReportRow[] source = observations.ToArray();
        if (source.Any(static observation => observation == null)) {
            throw new ArgumentException("Observations cannot contain null rows.", nameof(observations));
        }
        if (source.Length > options.MaximumObservations) {
            return Incomplete(
                $"Observation count {source.Length:N0} exceeds MaximumObservations {options.MaximumObservations:N0}.");
        }
        List<WorkingGroup> transport = options.Mode == EventDuplicateMode.None
            ? CreateSingletons(source)
            : CreateTransportGroups(source);
        List<WorkingGroup> grouped = options.Mode == EventDuplicateMode.Semantic
            ? CreateSemanticGroups(transport, options.Window)
            : transport;
        if (grouped.Count > options.MaximumGroups) {
            return Incomplete(
                $"Occurrence group count {grouped.Count:N0} exceeds MaximumGroups {options.MaximumGroups:N0}.");
        }
        EventOccurrenceGroup[] results = grouped
            .Select(CreateResult)
            .OrderBy(static group => group.Representative.TimeCreated)
            .ThenBy(static group => group.Identity, StringComparer.Ordinal)
            .ToArray();
        return new EventOccurrenceResult(results, isComplete: true, diagnostic: null);
    }

    private static List<WorkingGroup> CreateSingletons(IReadOnlyList<EventReportRow> source) =>
        CreateStableSingletons(
            source,
            "source-observation",
            "Grouping disabled; source observation retained.",
            "source");

    private static List<WorkingGroup> CreateTransportGroups(IReadOnlyList<EventReportRow> source) {
        var groups = new Dictionary<string, List<EventReportRow>>(StringComparer.Ordinal);
        var unkeyed = new List<EventReportRow>();
        foreach (EventReportRow observation in source) {
            if (!TryGetTransportIdentity(observation, out string identity)) {
                unkeyed.Add(observation);
                continue;
            }
            if (!groups.TryGetValue(identity, out List<EventReportRow>? values)) {
                values = new List<EventReportRow>();
                groups.Add(identity, values);
            }
            values.Add(observation);
        }
        var result = groups.Select(static group => new WorkingGroup(
            "transport-identity",
            1,
            "Same source computer, source channel, record ID, provider, event ID, and timestamp.",
            group.Key,
            group.Value)).ToList();
        result.AddRange(CreateStableSingletons(
            unkeyed,
            "source-observation",
            "Record ID unavailable; transport identity was not inferred.",
            "unkeyed"));
        return result;
    }

    private static List<WorkingGroup> CreateStableSingletons(
        IEnumerable<EventReportRow> source,
        string policyName,
        string reason,
        string identityPrefix) {

        var result = new List<WorkingGroup>();
        foreach (IGrouping<string, EventReportRow> group in source
                     .GroupBy(CreateObservationFingerprint, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
            int ordinal = 0;
            foreach (EventReportRow observation in group) {
                result.Add(new WorkingGroup(
                    policyName,
                    1,
                    reason,
                    string.Join("\0", identityPrefix, group.Key, ordinal.ToString(CultureInfo.InvariantCulture)),
                    new[] { observation }));
                ordinal++;
            }
        }
        return result;
    }

    private static List<WorkingGroup> CreateSemanticGroups(
        IReadOnlyList<WorkingGroup> source,
        TimeSpan window) {

        var semantic = new Dictionary<string, List<PolicyCandidate>>(StringComparer.Ordinal);
        var retained = new List<WorkingGroup>();
        foreach (WorkingGroup group in source) {
            EventReportRow representative = SelectRepresentative(group.Observations);
            PolicyCandidate? candidate = null;
            foreach (IEventOccurrencePolicy policy in Policies) {
                if (policy.TryGetIdentity(representative, out string identity, out string reason)) {
                    candidate = new PolicyCandidate(group, policy, identity, reason);
                    break;
                }
            }
            if (candidate == null) {
                retained.Add(group);
                continue;
            }
            string key = candidate.Policy.Name + "\0" + candidate.Policy.Version + "\0" + candidate.Identity;
            if (!semantic.TryGetValue(key, out List<PolicyCandidate>? values)) {
                values = new List<PolicyCandidate>();
                semantic.Add(key, values);
            }
            values.Add(candidate);
        }
        foreach (List<PolicyCandidate> candidates in semantic.Values) {
            PolicyCandidate[] ordered = candidates
                .OrderBy(static candidate => candidate.FirstEventUtc)
                .ThenBy(static candidate => candidate.Group.Identity, StringComparer.Ordinal)
                .ToArray();
            var partition = new List<PolicyCandidate>();
            DateTime partitionStart = default;
            foreach (PolicyCandidate candidate in ordered) {
                if (partition.Count > 0 && candidate.FirstEventUtc - partitionStart > window) {
                    retained.Add(CreateSemanticGroup(partition));
                    partition.Clear();
                }
                if (partition.Count == 0) {
                    partitionStart = candidate.FirstEventUtc;
                }
                partition.Add(candidate);
            }
            if (partition.Count > 0) {
                retained.Add(CreateSemanticGroup(partition));
            }
        }
        return retained;
    }

    private static WorkingGroup CreateSemanticGroup(IReadOnlyList<PolicyCandidate> candidates) {
        PolicyCandidate first = candidates[0];
        EventReportRow[] observations = candidates
            .SelectMany(static candidate => candidate.Group.Observations)
            .OrderBy(static observation => observation.TimeCreated)
            .ThenBy(static observation => observation.SourceComputer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static observation => observation.RecordId)
            .ThenBy(CreateObservationFingerprint, StringComparer.Ordinal)
            .ToArray();
        return new WorkingGroup(
            first.Policy.Name,
            first.Policy.Version,
            first.Reason,
            first.Identity,
            observations);
    }

    private static EventOccurrenceGroup CreateResult(WorkingGroup group) {
        EventReportRow[] observations = group.Observations
            .OrderBy(static observation => observation.TimeCreated)
            .ThenBy(static observation => observation.SourceComputer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static observation => observation.RecordId)
            .ThenBy(CreateObservationFingerprint, StringComparer.Ordinal)
            .ToArray();
        string identity = CreateIdentity(group, observations);
        return new EventOccurrenceGroup(
            identity,
            SelectRepresentative(observations),
            observations,
            group.PolicyName,
            group.PolicyVersion,
            group.Reason);
    }

    private static EventReportRow SelectRepresentative(IReadOnlyList<EventReportRow> observations) => observations
        .OrderByDescending(static observation => observation.NormalizedValues.Count(static value =>
            value.Value.Value != null && value.Value.DisplayValue.Length > 0))
        .ThenByDescending(static observation => IsDirect(observation))
        .ThenBy(static observation => observation.TimeCreated)
        .ThenBy(static observation => observation.SourceComputer, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static observation => observation.RecordId)
        .ThenBy(CreateObservationFingerprint, StringComparer.Ordinal)
        .First();

    private static bool TryGetTransportIdentity(EventReportRow observation, out string identity) {
        if (!observation.RecordId.HasValue || string.IsNullOrWhiteSpace(observation.SourceComputer) ||
            string.IsNullOrWhiteSpace(observation.SourceLog)) {
            identity = string.Empty;
            return false;
        }
        identity = string.Join("\0",
            observation.SourceComputer.Trim().ToUpperInvariant(),
            observation.SourceLog.Trim().ToUpperInvariant(),
            observation.RecordId.Value.ToString(CultureInfo.InvariantCulture),
            observation.Provider.Trim().ToUpperInvariant(),
            observation.EventId.ToString(CultureInfo.InvariantCulture),
            observation.TimeCreated.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    private static bool IsDirect(EventReportRow observation) =>
        string.Equals(observation.SourceLog, observation.ContainerLog, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(observation.SourceComputer, observation.CollectorComputer, StringComparison.OrdinalIgnoreCase);

    private static string CreateIdentity(WorkingGroup group, IReadOnlyList<EventReportRow> observations) {
        string input = string.Join("\n", new[] {
            group.PolicyName,
            group.PolicyVersion.ToString(CultureInfo.InvariantCulture),
            group.Identity
        }.Concat(observations.Select(CreateObservationFingerprint)));
        return Hash(input);
    }

    private static string CreateObservationFingerprint(EventReportRow observation) {
        string input = string.Join("\n", observation.ToDictionary()
            .OrderBy(static value => value.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value.Key, StringComparer.Ordinal)
            .Select(static value => value.Key.ToUpperInvariant() + "\0" +
                                    CanonicalizeExact(value.Value)));
        return Hash(input);
    }

    private static string CanonicalizeExact(object? value) {
        string type;
        string text;
        switch (value) {
            case null:
                type = "null";
                text = string.Empty;
                break;
            case DateTime date:
                type = "datetime";
                text = date.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
                break;
            case DateTimeOffset date:
                type = "datetime";
                text = date.UtcTicks.ToString(CultureInfo.InvariantCulture);
                break;
            case string valueText:
                type = "text";
                text = valueText.Normalize(NormalizationForm.FormC);
                break;
            case System.Collections.IDictionary dictionary:
                type = "dictionary";
                text = string.Join("\u001f", dictionary.Keys.Cast<object?>()
                    .Select(key => new {
                        Key = CanonicalizeExact(key),
                        Value = CanonicalizeExact(dictionary[key!])
                    })
                    .OrderBy(static item => item.Key, StringComparer.Ordinal)
                    .Select(static item => item.Key + "\u001e" + item.Value));
                break;
            case System.Collections.IEnumerable enumerable when value is not string:
                type = "collection";
                text = string.Join("\u001f", enumerable.Cast<object?>().Select(CanonicalizeExact));
                break;
            case IFormattable formattable:
                type = value.GetType().FullName ?? value.GetType().Name;
                text = formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
                break;
            default:
                type = value.GetType().FullName ?? value.GetType().Name;
                text = value.ToString() ?? string.Empty;
                break;
        }
        return type.Length.ToString(CultureInfo.InvariantCulture) + ":" + type +
               text.Length.ToString(CultureInfo.InvariantCulture) + ":" + text + "|";
    }

    private static string Hash(string input) {
        using SHA256 sha256 = SHA256.Create();
        return string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(input))
            .Select(static value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static EventOccurrenceResult Incomplete(string diagnostic) =>
        new(Array.Empty<EventOccurrenceGroup>(), isComplete: false, diagnostic);

    private static void Validate(EventOccurrenceOptions options) {
        if (!Enum.IsDefined(typeof(EventDuplicateMode), options.Mode)) {
            throw new ArgumentOutOfRangeException(nameof(options.Mode));
        }
        if (options.Window < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(options.Window));
        }
        if (options.MaximumObservations <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumObservations));
        }
        if (options.MaximumGroups <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumGroups));
        }
    }

    private sealed class WorkingGroup {
        internal WorkingGroup(
            string policyName,
            int policyVersion,
            string reason,
            string identity,
            IEnumerable<EventReportRow> observations) {

            PolicyName = policyName;
            PolicyVersion = policyVersion;
            Reason = reason;
            Identity = identity;
            Observations = observations.ToArray();
        }

        internal string PolicyName { get; }
        internal int PolicyVersion { get; }
        internal string Reason { get; }
        internal string Identity { get; }
        internal IReadOnlyList<EventReportRow> Observations { get; }
    }

    private sealed class PolicyCandidate {
        internal PolicyCandidate(
            WorkingGroup group,
            IEventOccurrencePolicy policy,
            string identity,
            string reason) {

            Group = group;
            Policy = policy;
            Identity = identity;
            Reason = reason;
        }

        internal WorkingGroup Group { get; }
        internal IEventOccurrencePolicy Policy { get; }
        internal string Identity { get; }
        internal string Reason { get; }
        internal DateTime FirstEventUtc => Group.Observations.Min(static observation => observation.TimeCreated).ToUniversalTime();
    }
}
