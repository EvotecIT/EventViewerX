namespace EventViewerX.Providers;

public static partial class EventProviderDefinitionValidator {
    private static readonly HashSet<string> StandardOpcodes =
        new(
            new[] {
                "win:Info",
                "win:Start",
                "win:Stop",
                "win:DC_Start",
                "win:DC_Stop",
                "win:Extension",
                "win:Reply",
                "win:Resume",
                "win:Suspend",
                "win:Send",
                "win:Receive",
                "win:ReservedOpcode241",
                "win:ReservedOpcode242",
                "win:ReservedOpcode243",
                "win:ReservedOpcode244",
                "win:ReservedOpcode245",
                "win:ReservedOpcode246",
                "win:ReservedOpcode247",
                "win:ReservedOpcode248",
                "win:ReservedOpcode249",
                "win:ReservedOpcode250",
                "win:ReservedOpcode251",
                "win:ReservedOpcode252",
                "win:ReservedOpcode253",
                "win:ReservedOpcode254",
                "win:ReservedOpcode255"
            },
            StringComparer.Ordinal);

    private static readonly HashSet<string> StandardKeywords =
        new(
            new[] {
                "win:AnyKeyword",
                "win:ResponseTime",
                "win:WDIContext",
                "win:WDIDiag",
                "win:SQM",
                "win:AuditFailure",
                "win:AuditSuccess",
                "win:CorrelationHint",
                "win:EventlogClassic",
                "win:ReservedKeyword56",
                "win:ReservedKeyword57",
                "win:ReservedKeyword58",
                "win:ReservedKeyword59",
                "win:ReservedKeyword60",
                "win:ReservedKeyword61",
                "win:ReservedKeyword62",
                "win:ReservedKeyword63"
            },
            StringComparer.Ordinal);

    private static bool IsStandardLevel(string level) {
        return new[] {
            "win:LogAlways",
            "win:Critical",
            "win:Error",
            "win:Warning",
            "win:Informational",
            "win:Verbose"
        }.Contains(level, StringComparer.Ordinal);
    }
}
