namespace EventViewerX;

/// <summary>Stable semantic kind assigned by deterministic event-value normalization.</summary>
public enum EventNormalizedValueKind {
    /// <summary>No narrower semantic kind is known.</summary>
    Unknown,
    /// <summary>Ordinary text.</summary>
    Text,
    /// <summary>Windows message resource identifier.</summary>
    ResourceIdentifier,
    /// <summary>Directory operation.</summary>
    DirectoryOperation,
    /// <summary>Bit-mask flag set.</summary>
    FlagSet,
    /// <summary>UTC date and time.</summary>
    DateTime,
    /// <summary>Windows security identifier.</summary>
    SecurityIdentifier,
    /// <summary>Globally unique identifier.</summary>
    Guid,
    /// <summary>LDAP distinguished name.</summary>
    DistinguishedName,
    /// <summary>Object identifier.</summary>
    ObjectIdentifier,
    /// <summary>Ordered collection of values.</summary>
    MultiValue
}
