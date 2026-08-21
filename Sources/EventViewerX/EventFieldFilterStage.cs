namespace EventViewerX;

/// <summary>Earliest stage at which an event field can be filtered without changing semantics.</summary>
public enum EventFieldFilterStage {
    /// <summary>The field can be selected by the Windows Event Log engine.</summary>
    Native,
    /// <summary>The field requires typed projection before comparison.</summary>
    Managed
}
