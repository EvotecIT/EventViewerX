namespace EventViewerX;

/// <summary>One normalized machine returned by target discovery.</summary>
public sealed class EventTargetInfo {
    /// <summary>Creates one immutable target.</summary>
    public EventTargetInfo(
        string computerName,
        EventTargetKind kind,
        string? domainName = null,
        string? forestName = null,
        string? siteName = null,
        bool? isGlobalCatalog = null) {

        if (string.IsNullOrWhiteSpace(computerName)) {
            throw new ArgumentException("Computer name cannot be empty.", nameof(computerName));
        }
        ComputerName = computerName.Trim().TrimEnd('.');
        Kind = kind;
        DomainName = Normalize(domainName);
        ForestName = Normalize(forestName);
        SiteName = Normalize(siteName);
        IsGlobalCatalog = isGlobalCatalog;
    }

    /// <summary>DNS or local machine name used by Event Log queries.</summary>
    public string ComputerName { get; }
    /// <summary>Target kind.</summary>
    public EventTargetKind Kind { get; }
    /// <summary>DNS domain name when the target is a domain controller.</summary>
    public string? DomainName { get; }
    /// <summary>DNS forest name when the target is a domain controller.</summary>
    public string? ForestName { get; }
    /// <summary>Active Directory site name when available.</summary>
    public string? SiteName { get; }
    /// <summary>Whether the domain controller advertises the global catalog role.</summary>
    public bool? IsGlobalCatalog { get; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim().TrimEnd('.');
}
