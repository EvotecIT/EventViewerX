namespace EventViewerX.Reporting;

/// <summary>Null handling for group keys and measure operands.</summary>
public enum EventAggregationNullPolicy {
    /// <summary>Exclude null values from the measure or row grouping.</summary>
    Exclude,
    /// <summary>Include null as one explicit unknown value.</summary>
    Include
}
