using System.Globalization;
using System.Xml;

namespace PSEventViewer;

public sealed partial class CmdletGetEVXEvent {
    /// <summary>
    /// Culture used when provider resources do not contain MessageCulture.
    /// Get-EVXEvent requests en-US by default, then falls back to the current UI culture
    /// so deterministic English is preferred without discarding locally available messages.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public CultureInfo? FallbackMessageCulture { get; set; } =
        CultureInfo.CurrentUICulture;

    /// <summary>
    /// One or more Get-WinEvent compatible hashtables containing LogName, Path, ProviderName, or combinations of them plus event predicates.
    /// Arbitrary keys target named EventData fields. SuppressHashFilter adds native exclusions.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ParameterSetName = "Hashtable")]
    public Hashtable[]? FilterHashtable { get; set; }

    /// <summary>
    /// A complete Windows Event Log QueryList XML document. This supports multi-channel Select
    /// and Suppress expressions without translating or weakening the supplied query.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ParameterSetName = "Xml")]
    public XmlDocument? FilterXml { get; set; }

    /// <summary>
    /// A native Windows Event Log XPath expression applied to every LogName or Path.
    /// This cannot be combined with the high-level filter parameters.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    public string? FilterXPath { get; set; }

    /// <summary>Reusable typed filter produced by New-EVXFilter or EventViewerX.</summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    [Parameter(Mandatory = true, ParameterSetName = "TypedFilter")]
    public object? Filter { get; set; }

    /// <summary>Credentials used for remote channel or structured queries.</summary>
    [Credential]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public PSCredential? Credential { get; set; }

    /// <summary>Authentication package used for remote Windows Event Log sessions.</summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>
    /// Native bookmark XML used as the seek origin. A bookmark targets one source or one
    /// structured QueryList session and cannot be fanned out across several independent sources.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public string? BookmarkXml { get; set; }

    /// <summary>
    /// Record offset relative to BookmarkXml. The default of one resumes after the bookmarked event.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public long BookmarkOffset { get; set; } = 1;

    /// <summary>
    /// Allows bookmark seek to continue when the exact bookmarked record is not in the result set.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public SwitchParameter IgnoreStaleBookmark { get; set; }

    /// <summary>
    /// Continues other independent sources when a channel, computer, or file query fails.
    /// Each isolated failure is emitted as a non-terminating PowerShell error.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public SwitchParameter ContinueOnError { get; set; }

    /// <summary>
    /// Allows a structured QueryList to continue when one path cannot be evaluated.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    public SwitchParameter TolerateQueryErrors { get; set; }

    /// <summary>
    /// Includes analytic and debug channels when LogName or ProviderName uses wildcard patterns.
    /// An explicitly named analytic or debug channel never requires Force.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public SwitchParameter Force { get; set; }

    /// <summary>
    /// Materializes a native bookmark for each returned event. This is disabled by
    /// default because bookmark creation adds native handle and render work per record.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public SwitchParameter IncludeBookmark { get; set; }
}
