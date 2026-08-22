namespace EventViewerX;

/// <summary>Scope selected for explicit event target discovery.</summary>
public enum EventTargetDiscoveryScope {
    /// <summary>Return only the local machine. This is the default.</summary>
    LocalMachine,
    /// <summary>Discover domain controllers in the local computer's domain.</summary>
    CurrentDomain,
    /// <summary>Discover domain controllers in every domain of the local computer's forest.</summary>
    CurrentForest,
    /// <summary>Discover domain controllers in a named domain.</summary>
    Domain,
    /// <summary>Discover domain controllers in every domain of a named forest.</summary>
    Forest
}
