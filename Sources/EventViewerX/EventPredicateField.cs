namespace EventViewerX;

/// <summary>Discoverable field entry used to build one reusable typed predicate.</summary>
public sealed class EventPredicateField {
    internal EventPredicateField(EventFieldDefinition definition) {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>Field metadata.</summary>
    public EventFieldDefinition Definition { get; }

    /// <summary>Stable field name.</summary>
    public string Name => Definition.Name;

    /// <summary>Human-friendly field label.</summary>
    public string DisplayName => Definition.DisplayName;

    /// <summary>Field purpose shown by discovery surfaces.</summary>
    public string Description => Definition.Description;

    /// <summary>CLR value type used by comparisons.</summary>
    public Type ValueType => Definition.ValueType;

    /// <summary>Earliest stage at which the field can be filtered safely.</summary>
    public EventFieldFilterStage FilterStage => Definition.FilterStage;

    /// <summary>Comparison operations supported by this field.</summary>
    public IReadOnlyList<EventPredicateOperator> SupportedOperators => Definition.SupportedOperators;

    /// <summary>Whether this field belongs to every typed event record.</summary>
    public bool IsCommon => Definition.IsCommon;

    /// <summary>Creates an equality comparison.</summary>
    public EventPredicate Equal(object? value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.Equal, value);

    /// <summary>Creates scalar inequality or matches a collection containing at least one differing item.</summary>
    public EventPredicate NotEqual(object? value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.NotEqual, value);

    /// <summary>Matches any supplied value.</summary>
    public EventPredicate In(params object?[] values) =>
        EventPredicate.Compare(Name, EventPredicateOperator.In, values);

    /// <summary>Excludes every supplied value.</summary>
    public EventPredicate NotIn(params object?[] values) =>
        EventPredicate.Compare(Name, EventPredicateOperator.NotIn, values);

    /// <summary>Matches a string or collection containing a value.</summary>
    public EventPredicate Contains(object? value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.Contains, value);

    /// <summary>Matches a string prefix.</summary>
    public EventPredicate StartsWith(string value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.StartsWith, value);

    /// <summary>Matches a string suffix.</summary>
    public EventPredicate EndsWith(string value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.EndsWith, value);

    /// <summary>Matches a shell-style wildcard pattern.</summary>
    public EventPredicate MatchesWildcard(string pattern) =>
        EventPredicate.Compare(Name, EventPredicateOperator.MatchesWildcard, pattern);

    /// <summary>Matches a regular expression.</summary>
    public EventPredicate MatchesRegex(string pattern) =>
        EventPredicate.Compare(Name, EventPredicateOperator.MatchesRegex, pattern);

    /// <summary>Matches values greater than the supplied value.</summary>
    public EventPredicate GreaterThan(object value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.GreaterThan, value);

    /// <summary>Matches values greater than or equal to the supplied value.</summary>
    public EventPredicate GreaterThanOrEqual(object value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.GreaterThanOrEqual, value);

    /// <summary>Matches values less than the supplied value.</summary>
    public EventPredicate LessThan(object value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.LessThan, value);

    /// <summary>Matches values less than or equal to the supplied value.</summary>
    public EventPredicate LessThanOrEqual(object value) =>
        EventPredicate.Compare(Name, EventPredicateOperator.LessThanOrEqual, value);

    /// <summary>Matches an absent or null value.</summary>
    public EventPredicate IsNull() =>
        EventPredicate.Compare(Name, EventPredicateOperator.IsNull);

    /// <summary>Matches a present non-null value.</summary>
    public EventPredicate IsNotNull() =>
        EventPredicate.Compare(Name, EventPredicateOperator.IsNotNull);

    /// <summary>Matches an IP address within a CIDR subnet.</summary>
    public EventPredicate InSubnet(string subnet) =>
        EventPredicate.Compare(Name, EventPredicateOperator.InSubnet, subnet);

    /// <summary>Matches an IP address within a CIDR subnet.</summary>
    public EventPredicate MatchesSubnet(string subnet) => InSubnet(subnet);
}
