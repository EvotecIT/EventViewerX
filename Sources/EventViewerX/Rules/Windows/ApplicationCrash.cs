namespace EventViewerX.Rules.Windows;

/// <summary>Application Error reported a faulting application (1000).</summary>
public sealed class ApplicationCrash : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1000 };
    /// <inheritdoc />
    public override string LogName => "Application";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Application Error" };
    /// <inheritdoc />
    public override EventType Type => EventType.ApplicationCrash;

    /// <summary>Faulting executable name.</summary>
    public string ApplicationName;
    /// <summary>Faulting application version.</summary>
    public string ApplicationVersion;
    /// <summary>Faulting module name.</summary>
    public string ModuleName;
    /// <summary>Faulting module version.</summary>
    public string ModuleVersion;
    /// <summary>Provider exception code, preserved in its original hexadecimal representation.</summary>
    public string ExceptionCode;
    /// <summary>Offset in the faulting module.</summary>
    public string FaultOffset;
    /// <summary>Faulting executable path.</summary>
    public string ApplicationPath;
    /// <summary>Faulting module path.</summary>
    public string ModulePath;
    /// <summary>Windows Error Reporting identifier.</summary>
    public string ReportId;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public ApplicationCrash(EventObject source) : base(source, nameof(ApplicationCrash)) {
        ApplicationName = Value("AppName", "NoNameA0");
        ApplicationVersion = Value("AppVersion", "NoNameA1");
        ModuleName = Value("ModuleName", "NoNameA3");
        ModuleVersion = Value("ModuleVersion", "NoNameA4");
        ExceptionCode = Value("ExceptionCode", "NoNameA6");
        FaultOffset = Value("FaultingOffset", "NoNameA7");
        ApplicationPath = Value("AppPath", "NoNameA10");
        ModulePath = Value("ModulePath", "NoNameA11");
        ReportId = Value("IntegratorReportId", "NoNameA12");
    }
}
