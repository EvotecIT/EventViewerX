namespace EventViewerX.Reporting;

/// <summary>Calendar bucket applied in the declared report timezone.</summary>
public enum EventAggregationBucket {
    /// <summary>No time bucket.</summary>
    None,
    /// <summary>Local calendar hour.</summary>
    Hour,
    /// <summary>Local calendar day.</summary>
    Day,
    /// <summary>Local calendar week beginning Monday.</summary>
    Week,
    /// <summary>Local calendar month.</summary>
    Month
}
