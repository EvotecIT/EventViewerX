namespace EventViewerX.Rules.Windows;

/// <summary>Application Hang reported an unresponsive application (1002).</summary>
public sealed class ApplicationHang : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1002 };
    /// <inheritdoc />
    public override string LogName => "Application";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Application Hang" };
    /// <inheritdoc />
    public override EventType Type => EventType.ApplicationHang;

    /// <summary>Unresponsive executable name.</summary>
    public string ApplicationName;
    /// <summary>Application version.</summary>
    public string ApplicationVersion;
    /// <summary>Unresponsive executable path.</summary>
    public string ApplicationPath;
    /// <summary>Provider hang classification, when present.</summary>
    public string HangType;
    /// <summary>Windows Error Reporting identifier.</summary>
    public string ReportId;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public ApplicationHang(EventObject source) : base(source, nameof(ApplicationHang)) {
        ApplicationName = Value("AppName", "NoNameA0");
        ApplicationVersion = Value("AppVersion", "NoNameA1");
        ApplicationPath = Value("ExeFileName", "NoNameA5");
        HangType = Value("HangType", "NoNameA9");
        ReportId = Value("ReportId", "NoNameA6");
    }
}
