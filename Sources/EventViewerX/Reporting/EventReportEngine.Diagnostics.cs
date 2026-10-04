namespace EventViewerX.Reporting;

public static partial class EventReportEngine {
    // Completion evidence remains bounded even if a damaged source emits a diagnostic for every record.
    // The caller's diagnostic sink still receives every original diagnostic.
    private sealed class SavedEventCompletenessDiagnostics {
        private const int MaximumSamples = 16;
        private const int MaximumSampleLength = 4096;
        private readonly object gate = new();
        private readonly HashSet<string> samples = new(StringComparer.Ordinal);
        private long count;
        private bool truncated;

        internal void Add(SavedEventReadDiagnostic diagnostic) {
            lock (gate) {
                count++;
                if (samples.Count == MaximumSamples) {
                    truncated = true;
                    return;
                }
                string sample = $"{diagnostic.Code?.Trim() ?? string.Empty}: {diagnostic.Message?.Trim() ?? string.Empty}".Trim(' ', ':');
                if (sample.Length > MaximumSampleLength) {
                    sample = sample.Substring(0, MaximumSampleLength) + "...";
                    truncated = true;
                }
                if (sample.Length > 0) {
                    samples.Add(sample);
                }
            }
        }

        internal string? Describe() {
            lock (gate) {
                if (count == 0) {
                    return null;
                }
                return "Saved-event parsing did not represent every source record or region: " +
                    string.Join(" ", samples.OrderBy(static sample => sample, StringComparer.Ordinal)) +
                    (truncated ? $" Details are sampled from {count:N0} completeness diagnostics." : string.Empty);
            }
        }
    }
}
