namespace EventViewerX;

/// <summary>Comparison operations supported by reusable typed event predicates.</summary>
public enum EventPredicateOperator {
    /// <summary>The field equals one value.</summary>
    Equal,
    /// <summary>A scalar differs from one value, or a collection contains at least one differing item.</summary>
    NotEqual,
    /// <summary>The field equals any supplied value.</summary>
    In,
    /// <summary>The field equals none of the supplied values.</summary>
    NotIn,
    /// <summary>A string or collection contains the supplied value.</summary>
    Contains,
    /// <summary>A string starts with the supplied value.</summary>
    StartsWith,
    /// <summary>A string ends with the supplied value.</summary>
    EndsWith,
    /// <summary>A string matches a shell-style wildcard pattern.</summary>
    MatchesWildcard,
    /// <summary>A string matches a regular expression.</summary>
    MatchesRegex,
    /// <summary>A value is greater than the supplied value.</summary>
    GreaterThan,
    /// <summary>A value is greater than or equal to the supplied value.</summary>
    GreaterThanOrEqual,
    /// <summary>A value is less than the supplied value.</summary>
    LessThan,
    /// <summary>A value is less than or equal to the supplied value.</summary>
    LessThanOrEqual,
    /// <summary>The field is null or absent.</summary>
    IsNull,
    /// <summary>The field is present and not null.</summary>
    IsNotNull,
    /// <summary>An IP address belongs to the supplied CIDR subnet.</summary>
    InSubnet
}
