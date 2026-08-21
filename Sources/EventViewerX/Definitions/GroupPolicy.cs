namespace EventViewerX;

/// <summary>
/// Represents a single Group Policy with its name, ID, and domain.
/// </summary>
public class GroupPolicy {
    /// <summary>Name of the Group Policy Object.</summary>
    public string GpoName { get; set; } = string.Empty;
    /// <summary>Identifier of the Group Policy Object.</summary>
    public string GpoId { get; set; } = string.Empty;
    /// <summary>Domain hosting the Group Policy Object.</summary>
    public string GpoDomain { get; set; } = string.Empty;
}