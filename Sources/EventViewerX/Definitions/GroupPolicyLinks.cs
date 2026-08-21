namespace EventViewerX;

/// <summary>
/// Represents a single Group Policy link with its display name, GUID, and distinguished name.
/// </summary>
public class GroupPolicyLinks {
    /// <summary>Display name of the link.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Unique identifier of the link.</summary>
    public string Guid { get; set; } = string.Empty;
    /// <summary>Distinguished name of the Active Directory object.</summary>
    public string DistinguishedName { get; set; } = string.Empty;
    /// <summary>Indicates whether the link is enabled.</summary>
    public bool IsEnabled { get; set; }
}