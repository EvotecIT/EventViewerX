using System.Globalization;
using System.Security.Cryptography;

namespace EventViewerX.Reporting;

/// <summary>Creates non-destructive transport and semantic occurrence groups.</summary>
public static class EventOccurrenceEngine {
    /// <summary>Validates occurrence grouping options without consuming any observations.</summary>
    public static void ValidateOptions(EventOccurrenceOptions options) {
        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }
        Validate(options);
    }

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
        var bounded = new List<EventReportRow>(Math.Min(options.MaximumObservations, 4096));
        foreach (EventReportRow observation in observations) {
            if (observation == null) {
                throw new ArgumentException("Observations cannot contain null rows.", nameof(observations));
            }
            if (bounded.Count >= options.MaximumObservations) {
                return Incomplete(
                    $"Observation count exceeds MaximumObservations {options.MaximumObservations:N0}.");
            }
            bounded.Add(observation);
        }
        EventReportRow[] source = bounded.ToArray();
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

        var candidates = new List<PolicyCandidate>();
        var retained = new List<WorkingGroup>();
        foreach (WorkingGroup group in source) {
            PolicyCandidate? candidate = null;
            foreach (IEventOccurrencePolicy policy in Policies) {
                IReadOnlyList<EventOccurrencePolicyIdentity> identities = GetPolicyIdentities(
                    policy,
                    group.Observations);
                if (identities.Count > 0) {
                    candidate = new PolicyCandidate(group, policy, identities);
                    break;
                }
            }
            if (candidate == null) {
                retained.Add(group);
                continue;
            }
            candidates.Add(candidate);
        }
        PolicyCandidate[] orderedCandidates = candidates
            .OrderBy(static candidate => candidate.FirstEventUtc)
            .ThenBy(static candidate => candidate.Group.Identity, StringComparer.Ordinal)
            .ToArray();
        var union = new CandidateUnion(orderedCandidates);
        var byIdentity = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        for (int index = 0; index < orderedCandidates.Length; index++) {
            PolicyCandidate candidate = orderedCandidates[index];
            foreach (EventOccurrencePolicyIdentity identity in candidate.Identities) {
                string key = candidate.Policy.Name + "\0" + candidate.Policy.Version + "\0" + identity.Identity;
                if (!byIdentity.TryGetValue(key, out List<int>? values)) {
                    values = new List<int>();
                    byIdentity.Add(key, values);
                }
                values.Add(index);
            }
        }
        foreach (List<int> values in byIdentity.Values) {
            int cluster = values[0];
            for (int index = 1; index < values.Count; index++) {
                int next = values[index];
                cluster = union.TryUnion(cluster, next, window)
                    ? union.Find(cluster)
                    : next;
            }
        }
        retained.AddRange(Enumerable.Range(0, orderedCandidates.Length)
            .GroupBy(union.Find)
            .OrderBy(static group => group.Key)
            .Select(group => CreateSemanticGroup(group
                .Select(index => orderedCandidates[index])
                .ToArray())));
        return retained;
    }

    private static IReadOnlyList<EventOccurrencePolicyIdentity> GetPolicyIdentities(
        IEventOccurrencePolicy policy,
        IReadOnlyList<EventReportRow> observations) {

        var identities = new SortedDictionary<string, EventOccurrencePolicyIdentity>(StringComparer.Ordinal);
        foreach (EventReportRow observation in observations) {
            IReadOnlyList<EventOccurrencePolicyIdentity> current =
                policy is IMultiIdentityEventOccurrencePolicy multiIdentity
                    ? multiIdentity.GetIdentities(observation)
                    : policy.TryGetIdentity(observation, out string singleIdentity, out string reason)
                        ? new[] { new EventOccurrencePolicyIdentity(singleIdentity, reason) }
                        : Array.Empty<EventOccurrencePolicyIdentity>();
            foreach (EventOccurrencePolicyIdentity policyIdentity in current) {
                if (!identities.TryGetValue(policyIdentity.Identity, out EventOccurrencePolicyIdentity? existing) ||
                    string.CompareOrdinal(policyIdentity.Reason, existing.Reason) < 0) {
                    identities[policyIdentity.Identity] = policyIdentity;
                }
            }
        }
        return identities.Values.ToArray();
    }

    private static WorkingGroup CreateSemanticGroup(IReadOnlyList<PolicyCandidate> candidates) {
        PolicyCandidate first = candidates
            .OrderBy(static candidate => candidate.FirstEventUtc)
            .ThenBy(static candidate => candidate.Group.Identity, StringComparer.Ordinal)
            .First();
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
            string.Join(" ", candidates
                .SelectMany(static candidate => candidate.Identities)
                .Select(static identity => identity.Reason)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static reason => reason, StringComparer.Ordinal)),
            string.Join("\u001f", candidates
                .SelectMany(static candidate => candidate.Identities)
                .Select(static identity => identity.Identity)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static identity => identity, StringComparer.Ordinal)),
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
            EventAggregationEngine.NormalizeDateTimeUtc(observation.TimeCreated).Ticks.ToString(CultureInfo.InvariantCulture));
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
        IEnumerable<KeyValuePair<string, object?>> common = new[] {
            new KeyValuePair<string, object?>(nameof(EventReportRow.TimeCreated), observation.TimeCreated),
            new KeyValuePair<string, object?>(nameof(EventReportRow.Type), observation.Type),
            new KeyValuePair<string, object?>(nameof(EventReportRow.EventId), observation.EventId),
            new KeyValuePair<string, object?>(nameof(EventReportRow.RecordId), observation.RecordId),
            new KeyValuePair<string, object?>(nameof(EventReportRow.Provider), observation.Provider),
            new KeyValuePair<string, object?>(nameof(EventReportRow.SourceLog), observation.SourceLog),
            new KeyValuePair<string, object?>(nameof(EventReportRow.ContainerLog), observation.ContainerLog),
            new KeyValuePair<string, object?>(nameof(EventReportRow.SourceKind), observation.SourceKind),
            new KeyValuePair<string, object?>(nameof(EventReportRow.SourceComputer), observation.SourceComputer),
            new KeyValuePair<string, object?>(nameof(EventReportRow.CollectorComputer), observation.CollectorComputer),
            new KeyValuePair<string, object?>(nameof(EventReportRow.Level), observation.Level),
            new KeyValuePair<string, object?>(nameof(EventReportRow.LevelValue), observation.LevelValue),
            new KeyValuePair<string, object?>(nameof(EventReportRow.ActivityId), observation.ActivityId),
            new KeyValuePair<string, object?>(nameof(EventReportRow.RelatedActivityId), observation.RelatedActivityId),
            new KeyValuePair<string, object?>(nameof(EventReportRow.Message), observation.Message)
        };
        string input = string.Join("\n", common
            .Select(static value => new { Namespace = "common", Value = value })
            .Concat(observation.Values.Select(static value => new { Namespace = "domain", Value = value }))
            .OrderBy(static value => value.Namespace, StringComparer.Ordinal)
            .ThenBy(static value => value.Value.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value.Value.Key, StringComparer.Ordinal)
            .Select(static value => value.Namespace + "\0" + value.Value.Key.ToUpperInvariant() + "\0" +
                                    CanonicalizeExact(value.Value.Value)));
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
                text = EventAggregationEngine.NormalizeDateTimeUtc(date).Ticks.ToString(CultureInfo.InvariantCulture);
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
            IReadOnlyList<EventOccurrencePolicyIdentity> identities) {

            Group = group;
            Policy = policy;
            Identities = identities;
        }

        internal WorkingGroup Group { get; }
        internal IEventOccurrencePolicy Policy { get; }
        internal IReadOnlyList<EventOccurrencePolicyIdentity> Identities { get; }
        internal DateTime FirstEventUtc => Group.Observations
            .Select(static observation => EventAggregationEngine.NormalizeDateTimeUtc(observation.TimeCreated))
            .Min();
        internal DateTime LastEventUtc => Group.Observations
            .Select(static observation => EventAggregationEngine.NormalizeDateTimeUtc(observation.TimeCreated))
            .Max();
    }

    private sealed class CandidateUnion {
        private readonly int[] _parents;
        private readonly DateTime[] _starts;
        private readonly DateTime[] _ends;

        internal CandidateUnion(IReadOnlyList<PolicyCandidate> candidates) {
            _parents = Enumerable.Range(0, candidates.Count).ToArray();
            _starts = candidates.Select(static candidate => candidate.FirstEventUtc).ToArray();
            _ends = candidates.Select(static candidate => candidate.LastEventUtc).ToArray();
        }

        internal int Find(int index) {
            while (_parents[index] != index) {
                _parents[index] = _parents[_parents[index]];
                index = _parents[index];
            }
            return index;
        }

        internal bool TryUnion(int left, int right, TimeSpan window) {
            int leftRoot = Find(left);
            int rightRoot = Find(right);
            if (leftRoot == rightRoot) {
                return true;
            }
            DateTime start = _starts[leftRoot] <= _starts[rightRoot]
                ? _starts[leftRoot]
                : _starts[rightRoot];
            DateTime end = _ends[leftRoot] >= _ends[rightRoot]
                ? _ends[leftRoot]
                : _ends[rightRoot];
            if (end - start > window) {
                return false;
            }
            int retained = Math.Min(leftRoot, rightRoot);
            int merged = Math.Max(leftRoot, rightRoot);
            _parents[merged] = retained;
            _starts[retained] = start;
            _ends[retained] = end;
            return true;
        }
    }
}
