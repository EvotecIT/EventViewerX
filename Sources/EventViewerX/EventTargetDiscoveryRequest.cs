using System.Net;

namespace EventViewerX;

/// <summary>Options for explicit, bounded event target discovery.</summary>
public sealed class EventTargetDiscoveryRequest {
    /// <summary>Discovery scope. The default returns only the local machine.</summary>
    public EventTargetDiscoveryScope Scope { get; set; } = EventTargetDiscoveryScope.LocalMachine;

    /// <summary>DNS name required by the named domain and forest scopes.</summary>
    public string? Name { get; set; }

    /// <summary>Whether explicitly requested forest discovery may traverse forest trusts.</summary>
    public bool IncludeTrustedForests { get; set; }

    /// <summary>Total bounded native topology budget.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum number of domains retained by one discovery operation.</summary>
    public int MaximumDomainCount { get; set; } = 64;

    /// <summary>Maximum number of distinct event-log targets retained by one discovery operation.</summary>
    public int MaximumTargetCount { get; set; } = 1024;

    /// <summary>Optional credential used only for a named domain or forest.</summary>
    public NetworkCredential? Credential { get; set; }

    internal EventTargetDiscoveryRequest Snapshot() {
        Validate();
        return new EventTargetDiscoveryRequest {
            Scope = Scope,
            Name = string.IsNullOrWhiteSpace(Name) ? null : Name!.Trim().Trim('.'),
            IncludeTrustedForests = IncludeTrustedForests,
            Timeout = Timeout,
            MaximumDomainCount = MaximumDomainCount,
            MaximumTargetCount = MaximumTargetCount,
            Credential = Credential == null
                ? null
                : new NetworkCredential(Credential.UserName, Credential.Password, Credential.Domain)
        };
    }

    internal void Validate() {
        if (!Enum.IsDefined(typeof(EventTargetDiscoveryScope), Scope)) {
            throw new ArgumentOutOfRangeException(nameof(Scope));
        }
        if (Timeout <= TimeSpan.Zero || Timeout.TotalMilliseconds > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be positive and fit within a 32-bit millisecond budget.");
        }
        if (MaximumDomainCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaximumDomainCount), "MaximumDomainCount must be positive.");
        }
        if (MaximumTargetCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaximumTargetCount), "MaximumTargetCount must be positive.");
        }
        bool namedScope = Scope == EventTargetDiscoveryScope.Domain || Scope == EventTargetDiscoveryScope.Forest;
        if (namedScope != !string.IsNullOrWhiteSpace(Name)) {
            throw new ArgumentException(
                namedScope
                    ? "Name is required for a named domain or forest scope."
                    : "Name can only be used with a named domain or forest scope.",
                nameof(Name));
        }
        if (Credential != null && !namedScope) {
            throw new ArgumentException("Credential can only be used with a named domain or forest scope.", nameof(Credential));
        }
        if (IncludeTrustedForests && Scope != EventTargetDiscoveryScope.CurrentForest && Scope != EventTargetDiscoveryScope.Forest) {
            throw new ArgumentException("Trusted forest traversal requires CurrentForest or Forest scope.", nameof(IncludeTrustedForests));
        }
    }
}
