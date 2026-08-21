namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Stops running EVX watchers by identifier, name, or en masse.</para>
/// <para type="description">Requires exactly one selector and reports missing identifiers or names instead of silently doing nothing. Use PassThru to return each watcher that was stopped.</para>
/// </summary>
/// <example>
///   <summary>Stop by Id</summary>
///   <code>Stop-EVXWatcher -Id 7b4b6d2c-6c2e-47e1-9c3a-1b5a0a4b9d11</code>
///   <para>Stops the watcher with the specified identifier.</para>
/// </example>
/// <example>
///   <summary>Stop by name</summary>
///   <code>Stop-EVXWatcher -Name SecurityWatcher -PassThru</code>
///   <para>Stops and returns all watchers whose name matches SecurityWatcher.</para>
/// </example>
/// <example>
///   <summary>Stop everything</summary>
///   <code>Stop-EVXWatcher -All -Confirm:$false</code>
///   <para>Stops every running watcher after ShouldProcess confirmation.</para>
/// </example>
[Cmdlet(
    VerbsLifecycle.Stop,
    "EVXWatcher",
    DefaultParameterSetName = ByIdParameterSet,
    SupportsShouldProcess = true,
    ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(WatcherInfo))]
public sealed class CmdletStopEVXWatcher : PSCmdlet {
    private const string ByIdParameterSet = "ById";
    private const string ByNameParameterSet = "ByName";
    private const string AllParameterSet = "All";

    /// <summary>Identifiers of watchers to stop.</summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = ByIdParameterSet)]
    [ValidateNotNullOrEmpty]
    public Guid[] Id { get; set; } = Array.Empty<Guid>();

    /// <summary>Name of the watchers to stop.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ByNameParameterSet)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stops all running watchers.</summary>
    [Parameter(Mandatory = true, ParameterSetName = AllParameterSet)]
    public SwitchParameter All { get; set; }

    /// <summary>Returns each watcher that was stopped.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        IReadOnlyCollection<WatcherInfo> candidates =
            ParameterSetName switch {
                ByIdParameterSet => GetByIds(),
                ByNameParameterSet =>
                    WatcherManager.GetWatchers(Name.Trim()),
                AllParameterSet => WatcherManager.GetWatchers(),
                _ => throw new PSInvalidOperationException(
                    $"Unsupported parameter set '{ParameterSetName}'.")
            };

        if (candidates.Count == 0) {
            object target = ParameterSetName == ByNameParameterSet
                ? Name
                : ParameterSetName == AllParameterSet
                    ? "all watchers"
                    : string.Join(", ", Id);
            WriteError(new ErrorRecord(
                new KeyNotFoundException(
                    $"No running watcher matched '{target}'."),
                "EVXWatcherNotFound",
                ErrorCategory.ObjectNotFound,
                target));
            return;
        }

        foreach (WatcherInfo watcher in candidates) {
            string target = string.IsNullOrWhiteSpace(watcher.Name)
                ? watcher.Id.ToString()
                : $"{watcher.Name} ({watcher.Id})";
            if (!ShouldProcess(target, "Stop event log watcher")) {
                continue;
            }
            if (WatcherManager.StopWatcher(watcher.Id) &&
                PassThru.IsPresent) {
                WriteObject(watcher);
            }
        }
    }

    private IReadOnlyCollection<WatcherInfo> GetByIds() {
        var requested = new HashSet<Guid>(Id);
        IReadOnlyCollection<WatcherInfo> matches = WatcherManager
            .GetWatchers()
            .Where(watcher => requested.Contains(watcher.Id))
            .ToArray();
        foreach (Guid missing in requested.Except(
                     matches.Select(static watcher => watcher.Id))) {
            WriteError(new ErrorRecord(
                new KeyNotFoundException(
                    $"No running watcher has identifier '{missing}'."),
                "EVXWatcherNotFound",
                ErrorCategory.ObjectNotFound,
                missing));
        }
        return matches;
    }
}
