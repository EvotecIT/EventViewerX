namespace EventViewerX;

/// <summary>Validates context contracts and resolves immutable facts without insertion-order dependence.</summary>
public static class EventContextResolver {
    /// <summary>Resolves a materialized fact set for one query.</summary>
    public static EventContextResolution Resolve(
        IEnumerable<EventContextFact> facts,
        EventContextQuery query) {

        if (facts == null) {
            throw new ArgumentNullException(nameof(facts));
        }
        EventContextQuery request = ValidateAndSnapshot(query);
        EventContextFact[] candidates = facts
            .Select(ValidateAndSnapshot)
            .Where(fact => fact.ObjectKind == request.ObjectKind)
            .Where(fact => IsVisible(fact, request.AuthorizationContext))
            .ToArray();
        HashSet<string> matchingCanonicalIds = candidates
            .Where(fact => Matches(fact, request))
            .Select(static fact => fact.CanonicalId)
            .ToHashSet(StringComparer.Ordinal);
        if (matchingCanonicalIds.Count > 1) {
            return new EventContextResolution {
                ObjectKind = request.ObjectKind,
                State = EventContextState.Ambiguous,
                Reason = "The requested alias is associated with more than one canonical object identity."
            };
        }
        EventContextFact[] visible = candidates
            .Where(fact => matchingCanonicalIds.Contains(fact.CanonicalId))
            .OrderBy(static fact => fact.EffectiveAtUtc)
            .ThenBy(static fact => fact.SourceIdentity, StringComparer.Ordinal)
            .ToArray();
        if (visible.Length == 0) {
            return new EventContextResolution {
                ObjectKind = request.ObjectKind,
                CanonicalId = NormalizeOptionalCanonical(request),
                State = EventContextState.Unknown,
                Reason = "No visible context fact matches the requested identity."
            };
        }

        EventContextFact[] applicable = visible
            .Where(fact => fact.EffectiveAtUtc <= request.AtUtc)
            .ToArray();
        if (applicable.Length == 0) {
            return new EventContextResolution {
                ObjectKind = request.ObjectKind,
                CanonicalId = visible[0].CanonicalId,
                State = EventContextState.Unknown,
                Reason = "Matching context exists only after the requested event time."
            };
        }

        DateTime decisiveTime = applicable.Max(static fact => fact.EffectiveAtUtc);
        EventContextFact[] decisive = applicable.Where(fact => fact.EffectiveAtUtc == decisiveTime).ToArray();
        string[] decisiveNames = DistinctValues(decisive.Select(static fact => fact.DisplayName));
        string[] decisiveDns = DistinctValues(decisive.Select(static fact => fact.DistinguishedName));
        bool contradictoryDeletion = decisive.Any(static fact => fact.IsDeleted) &&
                                     decisive.Any(static fact => !fact.IsDeleted);
        bool ambiguous = contradictoryDeletion || decisiveNames.Length > 1 || decisiveDns.Length > 1;
        string? nameAtTime = LatestDistinctValue(applicable, static fact => fact.DisplayName, out bool nameAmbiguous);
        string? distinguishedName = LatestDistinctValue(
            applicable,
            static fact => fact.DistinguishedName,
            out bool distinguishedNameAmbiguous);
        ambiguous |= nameAmbiguous || distinguishedNameAmbiguous;
        EventContextFact latestStored = visible[visible.Length - 1];
        string? currentName = null;
        if (!latestStored.IsDeleted) {
            currentName = LatestDistinctValue(visible, static fact => fact.DisplayName, out bool currentAmbiguous);
            ambiguous |= currentAmbiguous;
        }
        EventContextFact selected = decisive[decisive.Length - 1];
        EventContextState state = ambiguous
            ? EventContextState.Ambiguous
            : selected.IsDeleted
                ? EventContextState.Deleted
                : request.AtUtc < visible.Max(static fact => fact.EffectiveAtUtc)
                    ? EventContextState.Historical
                    : EventContextState.Current;
        return new EventContextResolution {
            ObjectKind = request.ObjectKind,
            CanonicalId = selected.CanonicalId,
            State = state,
            NameAtEventTime = nameAtTime,
            LastKnownName = nameAtTime,
            CurrentName = state == EventContextState.Ambiguous ? null : currentName,
            DistinguishedName = distinguishedName,
            Domain = LatestDistinctValue(applicable, static fact => fact.Domain, out _),
            Provenance = selected.Provenance,
            Reason = ambiguous ? "Facts at the same effective point disagree about material object state." : null
        };
    }

    /// <summary>Validates and detaches one fact for safe storage.</summary>
    public static EventContextFact ValidateAndSnapshot(EventContextFact fact) {
        if (fact == null) {
            throw new ArgumentNullException(nameof(fact));
        }
        if (!Enum.IsDefined(typeof(EventContextObjectKind), fact.ObjectKind)) {
            throw new ArgumentOutOfRangeException(nameof(fact), "ObjectKind is not supported.");
        }
        if (!Enum.IsDefined(typeof(EventContextProvenance), fact.Provenance)) {
            throw new ArgumentOutOfRangeException(nameof(fact), "Provenance is not supported.");
        }
        if (fact.EffectiveAtUtc == default || fact.ObservedAtUtc == default) {
            throw new ArgumentException("EffectiveAtUtc and ObservedAtUtc are required.", nameof(fact));
        }
        if (fact.EffectiveAtUtc.Kind == DateTimeKind.Unspecified ||
            fact.ObservedAtUtc.Kind == DateTimeKind.Unspecified) {
            throw new ArgumentException("EffectiveAtUtc and ObservedAtUtc must declare a UTC or local kind.", nameof(fact));
        }
        if (string.IsNullOrWhiteSpace(fact.SourceIdentity) || string.IsNullOrWhiteSpace(fact.ProviderName)) {
            throw new ArgumentException("SourceIdentity and ProviderName are required.", nameof(fact));
        }
        if (fact.ProviderSchemaVersion < 1) {
            throw new ArgumentOutOfRangeException(nameof(fact), "ProviderSchemaVersion must be positive.");
        }
        if (!fact.IsShareable && string.IsNullOrWhiteSpace(fact.AuthorizationContext)) {
            throw new ArgumentException(
                "Non-shareable facts require an AuthorizationContext.",
                nameof(fact));
        }
        string canonicalId = EventContextIdentity.NormalizeCanonicalId(fact.ObjectKind, fact.CanonicalId);
        string[] aliases = (fact.Aliases ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(EventContextIdentity.NormalizeAlias)
            .Append(canonicalId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        return new EventContextFact {
            ObjectKind = fact.ObjectKind,
            CanonicalId = canonicalId,
            Aliases = aliases,
            DisplayName = NormalizeOptional(fact.DisplayName),
            Domain = NormalizeOptional(fact.Domain),
            DistinguishedName = NormalizeOptional(fact.DistinguishedName),
            EffectiveAtUtc = NormalizeUtc(fact.EffectiveAtUtc),
            ObservedAtUtc = NormalizeUtc(fact.ObservedAtUtc),
            IsDeleted = fact.IsDeleted,
            Provenance = fact.Provenance,
            SourceIdentity = fact.SourceIdentity.Trim(),
            ProviderName = fact.ProviderName.Trim(),
            ProviderSchemaVersion = fact.ProviderSchemaVersion,
            ConfidenceReason = NormalizeOptional(fact.ConfidenceReason),
            AuthorizationContext = NormalizeAuthorizationContext(fact.AuthorizationContext),
            IsShareable = fact.IsShareable
        };
    }

    /// <summary>Validates and detaches one query for safe execution.</summary>
    public static EventContextQuery ValidateAndSnapshot(EventContextQuery query) {
        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (query.AtUtc == default) {
            throw new ArgumentException("AtUtc is required.", nameof(query));
        }
        if (!Enum.IsDefined(typeof(EventContextObjectKind), query.ObjectKind)) {
            throw new ArgumentOutOfRangeException(nameof(query), "ObjectKind is not supported.");
        }
        if (query.AtUtc.Kind == DateTimeKind.Unspecified) {
            throw new ArgumentException("AtUtc must declare a UTC or local kind.", nameof(query));
        }
        if (string.IsNullOrWhiteSpace(query.CanonicalId) && string.IsNullOrWhiteSpace(query.Alias)) {
            throw new ArgumentException("CanonicalId or Alias is required.", nameof(query));
        }
        return new EventContextQuery {
            ObjectKind = query.ObjectKind,
            CanonicalId = string.IsNullOrWhiteSpace(query.CanonicalId)
                ? null
                : EventContextIdentity.NormalizeCanonicalId(query.ObjectKind, query.CanonicalId!),
            Alias = string.IsNullOrWhiteSpace(query.Alias)
                ? null
                : EventContextIdentity.NormalizeAlias(query.Alias!),
            AtUtc = NormalizeUtc(query.AtUtc),
            AuthorizationContext = NormalizeAuthorizationContext(query.AuthorizationContext)
        };
    }

    private static bool Matches(EventContextFact fact, EventContextQuery query) {
        if (!string.IsNullOrWhiteSpace(query.CanonicalId) &&
            string.Equals(fact.CanonicalId, query.CanonicalId, StringComparison.Ordinal)) {
            return true;
        }
        return !string.IsNullOrWhiteSpace(query.Alias) && fact.Aliases.Contains(query.Alias!, StringComparer.Ordinal);
    }

    private static bool IsVisible(EventContextFact fact, string? authorizationContext) =>
        fact.IsShareable || string.Equals(
            fact.AuthorizationContext,
            authorizationContext,
            StringComparison.Ordinal);

    private static string? NormalizeOptionalCanonical(EventContextQuery query) => query.CanonicalId;

    private static DateTime NormalizeUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : value.ToUniversalTime();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static string? NormalizeAuthorizationContext(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim().ToUpperInvariant();

    private static string[] DistinctValues(IEnumerable<string?> values) => values
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .Select(static value => value!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string? LatestDistinctValue(
        IReadOnlyList<EventContextFact> facts,
        Func<EventContextFact, string?> selector,
        out bool ambiguous) {

        foreach (IGrouping<DateTime, EventContextFact> group in facts
                     .OrderByDescending(static fact => fact.EffectiveAtUtc)
                     .GroupBy(static fact => fact.EffectiveAtUtc)) {
            string[] values = DistinctValues(group.Select(selector));
            if (values.Length == 0) {
                continue;
            }
            ambiguous = values.Length > 1;
            return ambiguous ? null : values[0];
        }
        ambiguous = false;
        return null;
    }
}
