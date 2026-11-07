namespace EventViewerX.Rules.Windows;

/// <summary>Service Control Manager reported a start failure, timeout, termination error or unexpected termination.</summary>
public sealed class ServiceFailure : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 7000, 7009, 7023, 7031, 7034 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Service Control Manager" };
    /// <inheritdoc />
    public override EventType Type => EventType.ServiceFailure;

    /// <summary>Service display name supplied by SCM; this need not be the internal service name.</summary>
    public string ServiceName;
    /// <summary>StartFailure, StartTimeout, TerminationError or UnexpectedTermination.</summary>
    public string FailureKind;
    /// <summary>Provider error or message-resource token, when present.</summary>
    public string Error;
    /// <summary>SCM unexpected-termination count, when present.</summary>
    public long? FailureCount;
    /// <summary>Connection timeout for event 7009.</summary>
    public long? TimeoutMilliseconds;
    /// <summary>Delay before the recovery action announced by event 7031.</summary>
    public long? RecoveryDelayMilliseconds;
    /// <summary>Recovery action announced by SCM; this does not prove the action completed.</summary>
    public string RecoveryAction;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public ServiceFailure(EventObject source) : base(source, nameof(ServiceFailure)) {
        ServiceName = source.Id == 7009 ? Value("param2") : Value("param1");
        FailureKind = source.Id switch { 7000 => "StartFailure", 7009 => "StartTimeout", 7023 => "TerminationError", _ => "UnexpectedTermination" };
        Error = source.Id == 7000 || source.Id == 7023 ? Value("param2") : string.Empty;
        FailureCount = source.Id == 7031 || source.Id == 7034 ? Number("param2") : null;
        TimeoutMilliseconds = source.Id == 7009 ? Number("param1") : null;
        RecoveryDelayMilliseconds = source.Id == 7031 ? Number("param3") : null;
        RecoveryAction = source.Id == 7031 ? Value("param5") : string.Empty;
    }
}
