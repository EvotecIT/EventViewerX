namespace EventViewerX;

/// <summary>
/// Describes whether a Windows security descriptor has a DACL and whether it contains entries.
/// This is not an effective-access result.
/// </summary>
public enum SecurityDescriptorDaclState {
    /// <summary>The descriptor was unavailable or could not be parsed.</summary>
    Unavailable,
    /// <summary>The descriptor does not contain a DACL.</summary>
    NotPresent,
    /// <summary>The descriptor marks a DACL as present but its value is null.</summary>
    Null,
    /// <summary>The DACL exists but contains no access-control entries.</summary>
    Empty,
    /// <summary>The DACL contains one or more access-control entries.</summary>
    Entries
}
