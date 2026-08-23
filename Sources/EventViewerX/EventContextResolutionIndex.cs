namespace EventViewerX;

/// <summary>Indexes immutable context facts and resolves each canonical timeline once per batch.</summary>
internal static class EventContextResolutionIndex {
    internal static IReadOnlyList<EventContextResolution> Resolve(
        IReadOnlyList<EventContextFact> facts,
        IReadOnlyList<EventContextQuery> queries) {

        var results = new EventContextResolution[queries.Count];
        IEnumerable<IGrouping<ScopeKey, int>> scopes = Enumerable.Range(0, queries.Count)
            .GroupBy(index => new ScopeKey(
                queries[index].ObjectKind,
                queries[index].AuthorizationContext));
        foreach (IGrouping<ScopeKey, int> scopeGroup in scopes) {
            int firstIndex = scopeGroup.First();
            EventContextQuery first = queries[firstIndex];
            EventContextFact[] visible = facts
                .Where(fact => fact.ObjectKind == first.ObjectKind)
                .Where(fact => IsVisible(fact, first.AuthorizationContext))
                .ToArray();
            var scope = new ResolutionScope(visible);
            foreach (int index in scopeGroup) {
                results[index] = scope.Resolve(queries[index]);
            }
        }
        return results;
    }

    private static bool IsVisible(EventContextFact fact, string? authorizationContext) =>
        fact.IsShareable || string.Equals(
            fact.AuthorizationContext,
            authorizationContext,
            StringComparison.Ordinal);

    private sealed class ResolutionScope {
        private readonly Dictionary<string, EventContextTimeline> _timelines;
        private readonly Dictionary<string, Dictionary<string, DateTime>> _aliasCanonicalIds;

        internal ResolutionScope(IReadOnlyList<EventContextFact> facts) {
            _timelines = facts
                .GroupBy(static fact => fact.CanonicalId, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => new EventContextTimeline(group.ToArray()),
                    StringComparer.Ordinal);
            _aliasCanonicalIds = new Dictionary<string, Dictionary<string, DateTime>>(StringComparer.Ordinal);
            foreach (EventContextFact fact in facts) {
                foreach (string alias in fact.Aliases) {
                    if (!_aliasCanonicalIds.TryGetValue(alias, out Dictionary<string, DateTime>? canonicalIds)) {
                        canonicalIds = new Dictionary<string, DateTime>(StringComparer.Ordinal);
                        _aliasCanonicalIds.Add(alias, canonicalIds);
                    }
                    if (!canonicalIds.TryGetValue(fact.CanonicalId, out DateTime firstSeenUtc) ||
                        fact.EffectiveAtUtc < firstSeenUtc) {
                        canonicalIds[fact.CanonicalId] = fact.EffectiveAtUtc;
                    }
                }
            }
        }

        internal EventContextResolution Resolve(EventContextQuery query) {
            string? canonicalId = query.CanonicalId;
            if (!string.IsNullOrWhiteSpace(canonicalId)) {
                string[] aliasCanonicalIds = ResolveAliasCanonicalIds(query.Alias, query.AtUtc);
                if (!string.IsNullOrWhiteSpace(query.Alias) &&
                    aliasCanonicalIds.Length > 0 &&
                    !aliasCanonicalIds.Contains(canonicalId!, StringComparer.Ordinal)) {
                    return Ambiguous(
                        query,
                        canonicalId,
                        "The supplied canonical identity and alias identify different objects.");
                }
                if (!_timelines.TryGetValue(canonicalId!, out EventContextTimeline? canonicalTimeline) ||
                    !string.IsNullOrWhiteSpace(query.Alias) &&
                    !aliasCanonicalIds.Contains(canonicalId!, StringComparer.Ordinal)) {
                    return Unknown(query, canonicalId, "No visible context fact matches the requested identity.");
                }
                return canonicalTimeline.Resolve(query);
            }

            string[] matches = ResolveAliasCanonicalIds(query.Alias, query.AtUtc);
            if (matches.Length == 0) {
                return Unknown(query, null, "No visible context fact matches the requested identity.");
            }
            if (matches.Length > 1) {
                return Ambiguous(
                    query,
                    null,
                    "The requested alias is associated with more than one canonical object identity.");
            }
            string matchedCanonicalId = matches.Single();
            return _timelines[matchedCanonicalId].Resolve(query);
        }

        private string[] ResolveAliasCanonicalIds(string? alias, DateTime atUtc) {
            if (string.IsNullOrWhiteSpace(alias) ||
                !_aliasCanonicalIds.TryGetValue(alias!, out Dictionary<string, DateTime>? canonicalIds)) {
                return Array.Empty<string>();
            }
            return canonicalIds
                .Where(pair => pair.Value <= atUtc)
                .Select(static pair => pair.Key)
                .ToArray();
        }

        private static EventContextResolution Unknown(
            EventContextQuery query,
            string? canonicalId,
            string reason) => new() {
            ObjectKind = query.ObjectKind,
            CanonicalId = canonicalId,
            State = EventContextState.Unknown,
            Reason = reason
        };

        private static EventContextResolution Ambiguous(
            EventContextQuery query,
            string? canonicalId,
            string reason) => new() {
            ObjectKind = query.ObjectKind,
            CanonicalId = canonicalId,
            State = EventContextState.Ambiguous,
            Reason = reason
        };
    }

    private sealed class EventContextTimeline {
        private readonly TimelinePoint[] _points;
        private readonly string? _currentName;

        internal EventContextTimeline(IReadOnlyList<EventContextFact> facts) {
            _points = BuildPoints(facts);
            TimelinePoint latest = _points[_points.Length - 1];
            _currentName = latest.IsDeleted || latest.MaterialAmbiguous
                ? null
                : latest.Name;
        }

        internal EventContextResolution Resolve(EventContextQuery query) {
            int pointIndex = FindPointAtOrBefore(query.AtUtc);
            if (pointIndex < 0) {
                return new EventContextResolution {
                    ObjectKind = query.ObjectKind,
                    CanonicalId = _points[0].CanonicalId,
                    State = EventContextState.Unknown,
                    Reason = "Matching context exists only after the requested event time."
                };
            }
            TimelinePoint point = _points[pointIndex];
            EventContextState state = point.MaterialAmbiguous
                ? EventContextState.Ambiguous
                : point.IsDeleted
                    ? EventContextState.Deleted
                    : query.AtUtc < _points[_points.Length - 1].EffectiveAtUtc
                        ? EventContextState.Historical
                        : EventContextState.Current;
            return new EventContextResolution {
                ObjectKind = query.ObjectKind,
                CanonicalId = point.CanonicalId,
                State = state,
                NameAtEventTime = point.NameAmbiguous ? null : point.Name,
                LastKnownName = point.LastKnownName,
                CurrentName = _currentName,
                DistinguishedName = point.DistinguishedNameAmbiguous ? null : point.DistinguishedName,
                Domain = point.DomainAmbiguous ? null : point.Domain,
                Provenance = point.Provenance,
                Reason = point.MaterialAmbiguous
                    ? "Facts at the same effective point disagree about material object state."
                    : null
            };
        }

        private int FindPointAtOrBefore(DateTime atUtc) {
            int low = 0;
            int high = _points.Length - 1;
            int result = -1;
            while (low <= high) {
                int middle = low + ((high - low) / 2);
                if (_points[middle].EffectiveAtUtc <= atUtc) {
                    result = middle;
                    low = middle + 1;
                } else {
                    high = middle - 1;
                }
            }
            return result;
        }

        private static TimelinePoint[] BuildPoints(IReadOnlyList<EventContextFact> facts) {
            var points = new List<TimelinePoint>();
            string? name = null;
            string? lastKnownName = null;
            string? distinguishedName = null;
            string? domain = null;
            bool nameAmbiguous = false;
            bool distinguishedNameAmbiguous = false;
            bool domainAmbiguous = false;
            bool previousMayBeDeleted = false;
            bool hasPrevious = false;

            foreach (IGrouping<DateTime, EventContextFact> group in facts
                         .OrderBy(static fact => fact.EffectiveAtUtc)
                         .ThenBy(static fact => fact.SourceIdentity, StringComparer.Ordinal)
                         .GroupBy(static fact => fact.EffectiveAtUtc)) {
                EventContextFact[] groupFacts = group.ToArray();
                bool hasDeleted = groupFacts.Any(static fact => fact.IsDeleted);
                bool hasLive = groupFacts.Any(static fact => !fact.IsDeleted);
                bool deletionAmbiguous = hasDeleted && hasLive;
                bool isDeleted = hasDeleted && !hasLive;

                if (hasPrevious && previousMayBeDeleted && hasLive && !deletionAmbiguous) {
                    name = null;
                    nameAmbiguous = false;
                }

                EventContextFact[] nameObservations = groupFacts
                    .Where(static fact => fact.DisplayNameObserved)
                    .ToArray();
                if (nameObservations.Length > 0) {
                    string[] names = DistinctValues(nameObservations.Select(static fact => fact.DisplayName));
                    bool hasExplicitRemoval = nameObservations.Any(static fact => fact.DisplayName == null);
                    int variants = names.Length + (hasExplicitRemoval ? 1 : 0);
                    nameAmbiguous = variants > 1;
                    name = variants == 1 && !hasExplicitRemoval ? names[0] : null;
                    if (!nameAmbiguous && name != null) {
                        lastKnownName = name;
                    }
                }

                ApplyOptionalObservation(
                    groupFacts.Select(static fact => fact.DistinguishedName),
                    ref distinguishedName,
                    ref distinguishedNameAmbiguous);
                ApplyOptionalObservation(
                    groupFacts.Select(static fact => fact.Domain),
                    ref domain,
                    ref domainAmbiguous);

                EventContextFact selected = groupFacts[groupFacts.Length - 1];
                points.Add(new TimelinePoint {
                    EffectiveAtUtc = group.Key,
                    CanonicalId = selected.CanonicalId,
                    Name = name,
                    LastKnownName = lastKnownName,
                    DistinguishedName = distinguishedName,
                    Domain = domain,
                    IsDeleted = isDeleted,
                    DeletionAmbiguous = deletionAmbiguous,
                    NameAmbiguous = nameAmbiguous,
                    DistinguishedNameAmbiguous = distinguishedNameAmbiguous,
                    DomainAmbiguous = domainAmbiguous,
                    Provenance = selected.Provenance
                });
                previousMayBeDeleted = hasDeleted;
                hasPrevious = true;
            }
            return points.ToArray();
        }

        private static void ApplyOptionalObservation(
            IEnumerable<string?> observations,
            ref string? current,
            ref bool ambiguous) {

            string[] values = DistinctValues(observations);
            if (values.Length == 0) {
                return;
            }
            ambiguous = values.Length > 1;
            current = ambiguous ? null : values[0];
        }

        private static string[] DistinctValues(IEnumerable<string?> values) => values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed class TimelinePoint {
        internal DateTime EffectiveAtUtc { get; set; }
        internal string CanonicalId { get; set; } = string.Empty;
        internal string? Name { get; set; }
        internal string? LastKnownName { get; set; }
        internal string? DistinguishedName { get; set; }
        internal string? Domain { get; set; }
        internal bool IsDeleted { get; set; }
        internal bool DeletionAmbiguous { get; set; }
        internal bool NameAmbiguous { get; set; }
        internal bool DistinguishedNameAmbiguous { get; set; }
        internal bool DomainAmbiguous { get; set; }
        internal EventContextProvenance Provenance { get; set; }
        internal bool MaterialAmbiguous => DeletionAmbiguous ||
                                           NameAmbiguous ||
                                           DistinguishedNameAmbiguous ||
                                           DomainAmbiguous;
    }

    private sealed class ScopeKey : IEquatable<ScopeKey> {
        internal ScopeKey(EventContextObjectKind objectKind, string? authorizationContext) {
            ObjectKind = objectKind;
            AuthorizationContext = authorizationContext;
        }

        private EventContextObjectKind ObjectKind { get; }
        private string? AuthorizationContext { get; }

        public bool Equals(ScopeKey? other) => other != null &&
                                               ObjectKind == other.ObjectKind &&
                                               string.Equals(
                                                   AuthorizationContext,
                                                   other.AuthorizationContext,
                                                   StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as ScopeKey);

        public override int GetHashCode() {
            unchecked {
                return ((int)ObjectKind * 397) ^
                       (AuthorizationContext == null
                           ? 0
                           : StringComparer.Ordinal.GetHashCode(AuthorizationContext));
            }
        }
    }
}
