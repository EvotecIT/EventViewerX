using System;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Normalizes and validates <see cref="SecurityEvtxQueryRequest"/> inputs with shared defaults/caps.
/// </summary>
internal static class SecurityEvtxQueryRequestNormalizer {
    /// <summary>
    /// Default scanned-events cap used when caller does not provide one.
    /// </summary>
    public const int DefaultMaxEventsScanned = 5000;

    /// <summary>
    /// Upper bound for scanned-events cap.
    /// </summary>
    public const int MaxEventsScannedCap = 200000;

    /// <summary>
    /// Default top rows count per aggregate dimension.
    /// </summary>
    public const int DefaultTop = 20;

    /// <summary>
    /// Upper bound for top rows count per aggregate dimension.
    /// </summary>
    public const int MaxTopCap = 100;

    /// <summary>
    /// Default sample size when samples are requested.
    /// </summary>
    public const int DefaultSampleSize = 20;

    /// <summary>
    /// Upper bound for sample size.
    /// </summary>
    public const int MaxSampleSizeCap = 200;

    /// <summary>
    /// Creates a normalized security EVTX request from optional/raw host values.
    /// </summary>
    /// <param name="filePath">EVTX file path.</param>
    /// <param name="startTimeUtc">Optional UTC lower bound.</param>
    /// <param name="endTimeUtc">Optional UTC upper bound.</param>
    /// <param name="maxEventsScanned">Optional requested scanned-events cap.</param>
    /// <param name="top">Optional requested top rows count per dimension.</param>
    /// <param name="includeSamples">Optional include samples flag. Defaults to <see langword="false"/>.</param>
    /// <param name="sampleSize">Optional sample size.</param>
    /// <param name="request">Normalized request.</param>
    /// <param name="error">Validation error when request cannot be created.</param>
    /// <returns><see langword="true"/> when request is valid; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(
        string? filePath,
        DateTime? startTimeUtc,
        DateTime? endTimeUtc,
        int? maxEventsScanned,
        int? top,
        bool? includeSamples,
        int? sampleSize,
        out SecurityEvtxQueryRequest request,
        out string? error) {
        request = new SecurityEvtxQueryRequest();

        var normalizedFilePath = filePath?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedFilePath)) {
            error = "filePath is required.";
            return false;
        }

        if (startTimeUtc.HasValue && endTimeUtc.HasValue && startTimeUtc.Value > endTimeUtc.Value) {
            error = "startTimeUtc must be less than or equal to endTimeUtc.";
            return false;
        }

        request.FilePath = normalizedFilePath!;
        request.StartTimeUtc = startTimeUtc;
        request.EndTimeUtc = endTimeUtc;
        request.MaxEventsScanned = NormalizeBoundedInt(maxEventsScanned, DefaultMaxEventsScanned, min: 1, max: MaxEventsScannedCap);
        request.Top = NormalizeBoundedInt(top, DefaultTop, min: 1, max: MaxTopCap);
        request.IncludeSamples = includeSamples ?? false;
        request.SampleSize = NormalizeBoundedInt(sampleSize, DefaultSampleSize, min: 1, max: MaxSampleSizeCap);
        error = null;
        return true;
    }

    private static int NormalizeBoundedInt(int? value, int fallback, int min, int max) {
        var effective = value ?? fallback;
        if (effective < min) {
            return min;
        }
        if (effective > max) {
            return max;
        }

        return effective;
    }
}
