namespace EventViewerX;

/// <summary>Culture-independent effective local audit-policy result.</summary>
public sealed class EffectiveAuditPolicyResult {
    internal EffectiveAuditPolicyResult(
        Guid subcategoryGuid,
        bool succeeded,
        EventAuditOutcome outcomes,
        int errorCode,
        string? message) {

        SubcategoryGuid = subcategoryGuid;
        Succeeded = succeeded;
        Outcomes = outcomes;
        ErrorCode = errorCode;
        Message = message;
    }

    /// <summary>Windows audit subcategory identifier.</summary>
    public Guid SubcategoryGuid { get; }
    /// <summary>Whether the effective policy was read.</summary>
    public bool Succeeded { get; }
    /// <summary>Effective success and failure auditing flags.</summary>
    public EventAuditOutcome Outcomes { get; }
    /// <summary>Win32 error code when the query failed.</summary>
    public int ErrorCode { get; }
    /// <summary>Diagnostic message when the query failed.</summary>
    public string? Message { get; }
}
