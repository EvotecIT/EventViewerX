using OfficeIMO.CSV;

namespace EventViewerX.Reporting;

/// <summary>Controls homogeneous typed CSV and CSV bundle output.</summary>
public sealed class EventReportCsvOptions {
    /// <summary>Field delimiter. The default is a comma.</summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>Whether formula-like text is escaped for spreadsheet safety.</summary>
    public bool EscapeFormulaValues { get; set; } = true;

    /// <summary>Whether a ZIP bundle contains a separate event-provenance CSV.</summary>
    public bool IncludeProvenance { get; set; } = true;

    /// <summary>Whether a ZIP bundle contains source coverage and failure status.</summary>
    public bool IncludeCoverage { get; set; } = true;

    internal CsvSaveOptions CreateSaveOptions() => new() {
        Delimiter = Delimiter,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        FormulaInjectionPolicy = EscapeFormulaValues
            ? CsvFormulaInjectionPolicy.Escape
            : CsvFormulaInjectionPolicy.Preserve,
        DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fffffffK",
        NewLine = "\r\n"
    };
}
