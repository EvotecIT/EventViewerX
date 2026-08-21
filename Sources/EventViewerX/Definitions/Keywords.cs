namespace EventViewerX;
/// <summary>
/// Well known keyword flags for event records.
/// </summary>
public enum Keywords : long {
    /// <summary>Audit failure keyword.</summary>
    AuditFailure = (long)4503599627370496,
    /// <summary>Audit success keyword.</summary>
    AuditSuccess = (long)9007199254740992,
    /// <summary>Correlation hint keyword.</summary>
    CorrelationHint2 = (long)18014398509481984,
    /// <summary>Classic event log keyword.</summary>
    EventLogClassic = (long)36028797018963968,
    /// <summary>Software quality metrics keyword.</summary>
    Sqm = (long)2251799813685248,
    /// <summary>Windows diagnostic infrastructure keyword.</summary>
    WdiDiagnostic = (long)1125899906842624,
    /// <summary>Windows diagnostic context keyword.</summary>
    WdiContext = (long)562949953421312,
    /// <summary>Response time keyword.</summary>
    ResponseTime = (long)281474976710656,
    /// <summary>No keywords.</summary>
    None = (long)0
}
