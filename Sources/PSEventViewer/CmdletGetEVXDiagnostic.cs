namespace PSEventViewer;

/// <summary>Reads one bounded captured-log batch or interprets a diagnostic code in its declared context.</summary>
/// <para>Checkpoints preserve complete-frame boundaries. The command never executes a collection command or changes the endpoint.</para>
[Cmdlet(VerbsCommon.Get, "EVXDiagnostic", DefaultParameterSetName = "Log")]
[OutputType(typeof(EventDiagnosticReadResult), typeof(EventDiagnosticCode))]
public sealed class CmdletGetEVXDiagnostic : AsyncPSCmdlet {
    /// <para>Log file to read, using UTF-8 or BOM-marked UTF-16.</para>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Log")]
    public string Path { get; set; } = string.Empty;
    /// <para>Previous batch restart position.</para>
    [Parameter(ParameterSetName = "Log")]
    public EventDiagnosticCheckpoint? Checkpoint { get; set; }
    /// <para>Finite bounds, declared writer offset, and final-file behavior.</para>
    [Parameter(ParameterSetName = "Log")]
    public EventDiagnosticReadOptions? Options { get; set; }
    /// <para>Signed decimal, unsigned decimal, or 0x-prefixed hexadecimal number.</para>
    [Parameter(Mandatory = true, ParameterSetName = "Code")]
    public string Code { get; set; } = string.Empty;
    /// <para>Emitting API family. Unknown preserves the number without guessing an outcome.</para>
    [Parameter(ParameterSetName = "Code")]
    public EventDiagnosticCodeKind Kind { get; set; } = EventDiagnosticCodeKind.Unknown;
    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        WriteObject(ParameterSetName == "Code" ? (object)EventDiagnosticCode.Resolve(Code, Kind)
            : EventDiagnosticLogReader.Read(SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path), Options, Checkpoint, CancelToken), false);
        return Task.CompletedTask;
    }
}