namespace EventViewerX.Sigma;

/// <summary>Controls optional, explicitly selected assumptions used while compiling Sigma rules.</summary>
public sealed class SigmaCompilationOptions {
    /// <summary>
    /// Gets or sets the telemetry profile used to resolve Sigma categories that do not carry an
    /// explicit event ID on every matching branch. The default is <see langword="null"/>, which
    /// preserves strict lossless compilation and rejects such categories.
    /// </summary>
    public SigmaLogSourceProfile? LogSourceProfile { get; set; }
}
