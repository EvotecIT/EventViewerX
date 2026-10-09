using System.Globalization;
using System.Text.Json.Serialization;

namespace EventViewerX;

/// <summary>Context of a diagnostic number; the same bits can have unrelated meanings in different APIs.</summary>
public enum EventDiagnosticCodeKind {
    /// <summary>No authoritative family is known.</summary>
    Unknown,
    /// <summary>Win32 status code.</summary>
    Win32,
    /// <summary>HRESULT bit layout.</summary>
    HResult,
    /// <summary>Windows Installer return code.</summary>
    WindowsInstaller,
    /// <summary>Application-defined process exit code. Requires the application's configured return-code map.</summary>
    ProcessExit
}

/// <summary>Independently authored interpretation, retaining the original representation and unknown outcomes.</summary>
public sealed partial class EventDiagnosticCode {
    /// <summary>Unmodified input.</summary>
    [JsonInclude]
    public string Original { get; private set; } = string.Empty;
    /// <summary>Declared context.</summary>
    [JsonInclude]
    public EventDiagnosticCodeKind Kind { get; private set; }
    /// <summary>Unsigned 32-bit representation, or null for invalid input.</summary>
    [JsonInclude]
    public uint? Value { get; private set; }
    /// <summary>Canonical hexadecimal representation.</summary>
    [JsonInclude]
    public string? Hex { get; private set; }
    /// <summary>HRESULT facility, only for a declared HRESULT.</summary>
    [JsonInclude]
    public int? Facility { get; private set; }
    /// <summary>HRESULT severity bit, only for a declared HRESULT.</summary>
    [JsonInclude]
    public bool? HResultFailure { get; private set; }
    /// <summary>Embedded Win32 value for a Win32-facility HRESULT.</summary>
    [JsonInclude]
    public uint? Win32Value { get; private set; }
    /// <summary>Success, SuccessRestartRequired, SuccessRestartInitiated, Failure, or Unknown.</summary>
    [JsonInclude]
    public string Outcome { get; private set; } = "Unknown";
    /// <summary>Meaning within the declared context; never an inferred root cause.</summary>
    [JsonInclude]
    public string Explanation { get; private set; } = string.Empty;
    /// <summary>Next artifact or configuration to inspect.</summary>
    [JsonInclude]
    public string NextCheck { get; private set; } = string.Empty;
    /// <summary>Primary documentation for the number family.</summary>
    [JsonInclude]
    public string Reference { get; private set; } = string.Empty;
    /// <summary>Documented constant or explicit HRESULT_FROM_WIN32 expression, when known in the declared family.</summary>
    [JsonInclude]
    public string? SymbolicName { get; private set; }

    /// <summary>Interprets signed decimal, unsigned decimal, or 0x-prefixed 32-bit hexadecimal. No context is guessed from magnitude.</summary>
    public static EventDiagnosticCode Resolve(string text, EventDiagnosticCodeKind kind = EventDiagnosticCodeKind.Unknown) {
        if (text == null) { throw new ArgumentNullException(nameof(text)); }
        if (!Enum.IsDefined(typeof(EventDiagnosticCodeKind), kind)) { throw new ArgumentOutOfRangeException(nameof(kind)); }
        var result = new EventDiagnosticCode { Original = text, Kind = kind };
        string input = text.Trim();
        uint value;
        bool valid = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(input.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
            : uint.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        if (!valid && int.TryParse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int signed)) { value = unchecked((uint)signed); valid = true; }
        if (!valid) { result.Explanation = "This value is not a 32-bit diagnostic number."; result.NextCheck = "Retain the original value and determine its emitting API."; return result; }
        result.Value = value; result.Hex = "0x" + value.ToString("X8", CultureInfo.InvariantCulture);
        result.Explanation = "The numeric representation is known; its outcome needs the emitting API or configured return-code map.";
        result.NextCheck = "Inspect the application's return-code mapping and the surrounding operation.";
        if (kind == EventDiagnosticCodeKind.HResult) {
            result.Facility = (int)((value >> 16) & 0x1fff);
            result.HResultFailure = (value & 0x80000000) != 0;
            result.Outcome = result.HResultFailure.Value ? "Failure" : "Success";
            result.Explanation = "The HRESULT severity bit indicates " + result.Outcome.ToLowerInvariant() + ". This is not a root-cause diagnosis.";
            if ((value & 0xffff0000) == 0x80070000) { result.Win32Value = value & 0xffff; }
            result.Reference = "https://learn.microsoft.com/en-us/windows/win32/com/structure-of-com-error-codes";
            result.NextCheck = "Inspect the failing API and its preceding diagnostic records.";
        } else if (kind is EventDiagnosticCodeKind.Win32 or EventDiagnosticCodeKind.WindowsInstaller) {
            result.Outcome = value == 0 ? "Success" : "Failure";
            result.Reference = "https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes";
            result.Explanation = value == 0 ? "The operation returned success." : "The API returned a nonzero status; inspect the operation that produced it.";
            result.NextCheck = "Inspect the operation's diagnostic context and platform documentation.";
            if (kind == EventDiagnosticCodeKind.WindowsInstaller) {
                result.Reference = "https://learn.microsoft.com/en-us/windows/win32/msi/error-codes";
                switch (value) {
                    case 1641: result.Outcome = "SuccessRestartInitiated"; break;
                    case 3010: result.Outcome = "SuccessRestartRequired"; break;
                }
                result.NextCheck = result.Outcome.StartsWith("SuccessRestart", StringComparison.Ordinal)
                    ? "Inspect reboot policy and post-restart detection before calling the application deployment complete."
                    : "Inspect the installer log and configured Intune return-code mapping.";
            }
        }
        ApplyKnownMeaning(result);
        return result;
    }
}