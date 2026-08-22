namespace EventViewerX;

/// <summary>Success and failure outcomes required from a Windows audit policy.</summary>
[Flags]
public enum EventAuditOutcome {
    /// <summary>No success or failure audit outcome applies.</summary>
    None = 0,
    /// <summary>Success auditing is required.</summary>
    Success = 1,
    /// <summary>Failure auditing is required.</summary>
    Failure = 2
}
