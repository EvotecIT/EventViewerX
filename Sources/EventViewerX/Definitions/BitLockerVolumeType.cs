namespace EventViewerX;

/// <summary>
/// Types of volumes protected by BitLocker.
/// </summary>
public enum BitLockerVolumeType {
    /// <summary>Boot or system volume.</summary>
    OperatingSystemVolume = 810,
    /// <summary>Internal fixed data volume.</summary>
    FixedDataVolume = 811,
    /// <summary>External/removable data volume.</summary>
    RemovableDataVolume = 812
}
