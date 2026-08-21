namespace EventViewerX;

/// <summary>Result of applying classic log desired state.</summary>
public sealed class ClassicEventLogEnsureResult {
    /// <summary>State before the operation.</summary>
    public ClassicLogState Before { get; internal set; } = null!;

    /// <summary>State after the operation.</summary>
    public ClassicLogState After { get; internal set; } = null!;

    /// <summary>Whether the operation created the log.</summary>
    public bool CreatedLog { get; internal set; }

    /// <summary>Whether the operation registered the source.</summary>
    public bool CreatedSource { get; internal set; }

    /// <summary>Whether size or overflow settings changed.</summary>
    public bool UpdatedConfiguration { get; internal set; }

    /// <summary>Whether post-operation verification confirmed the requested state.</summary>
    public bool Success { get; internal set; }

    /// <summary>Whether the operation changed anything.</summary>
    public bool Changed =>
        CreatedLog ||
        CreatedSource ||
        UpdatedConfiguration;
}
