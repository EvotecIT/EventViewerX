using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace EventViewerX;

public static partial class EventDiagnosticLogReader {
    private static readonly Regex Attributes = new("(?<key>[a-zA-Z]+)=\"(?<value>[^\"]*)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex TimeOffset = new("^(?<time>\\d{1,2}:\\d{2}:\\d{2}(?:\\.\\d{1,7})?)(?<offset>[+-]\\d{1,4})?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static EventDiagnosticRecord Parse(string raw, TimeSpan? suppliedOffset) {
        var record = new EventDiagnosticRecord { RawText = raw, Message = raw.TrimEnd('\r', '\n'), Format = "PlainText" };
        string text = raw.Trim();
        if (!text.StartsWith("<![LOG[", StringComparison.Ordinal)) { return record; }
        record.Format = "CMTrace";
        int end = text.IndexOf("]LOG]!>", 7, StringComparison.Ordinal);
        if (end < 0 || end + 7 >= text.Length || text[end + 7] != '<' || !text.EndsWith(">", StringComparison.Ordinal)) {
            record.Format = "Malformed"; record.Diagnostic = "CMTrace metadata is missing or incomplete."; return record;
        }
        record.Message = text.Substring(7, end - 7);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Attributes.Matches(text.Substring(end + 7))) {
            string key = match.Groups["key"].Value;
            if (metadata.ContainsKey(key)) { record.Format = "Malformed"; record.Diagnostic = "Duplicate CMTrace attribute."; return record; }
            metadata[key] = match.Groups["value"].Value;
        }
        string Get(string key) => metadata.TryGetValue(key, out string? value) ? value : string.Empty;
        record.Component = Get("component"); record.Thread = Get("thread"); record.Context = Get("context");
        record.Severity = int.TryParse(Get("type"), NumberStyles.None, CultureInfo.InvariantCulture, out int severity) ? severity : null;
        record.RawTime = Get("date") + " " + Get("time");
        Match time = TimeOffset.Match(Get("time"));
        if (!time.Success || !DateTime.TryParseExact(Get("date") + " " + time.Groups["time"].Value,
                new[] { "M-d-yyyy H:mm:ss.FFFFFFF", "M-d-yyyy H:mm:ss", "yyyy-MM-dd H:mm:ss.FFFFFFF", "yyyy-MM-dd H:mm:ss" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local)) {
            record.TimeQuality = "InvalidTime"; record.Diagnostic = "CMTrace date/time could not be parsed."; return record;
        }
        TimeSpan? offset = suppliedOffset;
        if (time.Groups["offset"].Success) {
            // CMTrace represents the signed UTC offset in minutes, not HHmm and not a collector-local zone.
            if (!int.TryParse(time.Groups["offset"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int minutes) || Math.Abs(minutes) > 840) {
                record.TimeQuality = "InvalidTime"; record.Diagnostic = "CMTrace offset exceeds the supported range."; return record;
            }
            offset = TimeSpan.FromMinutes(minutes);
            record.TimeQuality = "EmbeddedOffset";
        } else { record.TimeQuality = offset.HasValue ? "SuppliedOffset" : "UnknownOffset"; }
        if (offset.HasValue) {
            try { record.Timestamp = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), offset.Value); }
            catch (ArgumentException) { record.TimeQuality = "InvalidTime"; record.Diagnostic = "CMTrace timestamp is outside the supported instant range."; }
        }
        return record;
    }

    private static string PhysicalIdentity(FileStream stream, string path) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && GetFileInformationByHandle(stream.SafeFileHandle, out FileInformation info)) {
            return $"{info.VolumeSerial:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}";
        }
        return path + ":" + File.GetCreationTimeUtc(path).Ticks.ToString(CultureInfo.InvariantCulture);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerial;
        public uint SizeHigh;
        public uint SizeLow;
        public uint Links;
        public uint IndexHigh;
        public uint IndexLow;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle, out FileInformation information);
}