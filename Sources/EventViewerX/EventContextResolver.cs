namespace EventViewerX;

/// <summary>Validates context contracts and resolves immutable facts without insertion-order dependence.</summary>
public static class EventContextResolver {
    /// <summary>Resolves a materialized fact set for one query.</summary>
    public static EventContextResolution Resolve(
        IEnumerable<EventContextFact> facts,
        EventContextQuery query) {

        return ResolveMany(facts, new[] { query })[0];
    }

    /// <summary>
    /// Resolves multiple queries from one validated and indexed fact snapshot. Timelines are
    /// materialized once, so many event-time queries do not repeatedly scan the same history.
    /// </summary>
    public static IReadOnlyList<EventContextResolution> ResolveMany(
        IEnumerable<EventContextFact> facts,
        IReadOnlyList<EventContextQuery> queries) {

        if (facts == null) {
            throw new ArgumentNullException(nameof(facts));
        }
        if (queries == null) {
            throw new ArgumentNullException(nameof(queries));
        }
        EventContextFact[] snapshots = facts.Select(ValidateAndSnapshot).ToArray();
        EventContextQuery[] requests = queries.Select(ValidateAndSnapshot).ToArray();
        return EventContextResolutionIndex.Resolve(snapshots, requests);
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
            DisplayNameObserved = fact.DisplayNameObserved || !string.IsNullOrWhiteSpace(fact.DisplayName),
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

    private static DateTime NormalizeUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : value.ToUniversalTime();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static string? NormalizeAuthorizationContext(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim().ToUpperInvariant();

}
