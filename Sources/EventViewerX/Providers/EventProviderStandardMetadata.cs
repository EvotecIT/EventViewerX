namespace EventViewerX.Providers;

/// <summary>Numeric descriptors for standard Windows event metadata.</summary>
internal static class EventProviderStandardMetadata {
    private static readonly IReadOnlyDictionary<string, byte> Levels =
        new Dictionary<string, byte>(StringComparer.Ordinal) {
            ["win:LogAlways"] = 0,
            ["win:Critical"] = 1,
            ["win:Error"] = 2,
            ["win:Warning"] = 3,
            ["win:Informational"] = 4,
            ["win:Verbose"] = 5
        };

    private static readonly IReadOnlyDictionary<string, byte> Opcodes =
        new Dictionary<string, byte>(StringComparer.Ordinal) {
            ["win:Info"] = 0,
            ["win:Start"] = 1,
            ["win:Stop"] = 2,
            ["win:DC_Start"] = 3,
            ["win:DC_Stop"] = 4,
            ["win:Extension"] = 5,
            ["win:Reply"] = 6,
            ["win:Resume"] = 7,
            ["win:Suspend"] = 8,
            ["win:Send"] = 9,
            ["win:Receive"] = 240,
            ["win:ReservedOpcode241"] = 241,
            ["win:ReservedOpcode242"] = 242,
            ["win:ReservedOpcode243"] = 243,
            ["win:ReservedOpcode244"] = 244,
            ["win:ReservedOpcode245"] = 245,
            ["win:ReservedOpcode246"] = 246,
            ["win:ReservedOpcode247"] = 247,
            ["win:ReservedOpcode248"] = 248,
            ["win:ReservedOpcode249"] = 249,
            ["win:ReservedOpcode250"] = 250,
            ["win:ReservedOpcode251"] = 251,
            ["win:ReservedOpcode252"] = 252,
            ["win:ReservedOpcode253"] = 253,
            ["win:ReservedOpcode254"] = 254,
            ["win:ReservedOpcode255"] = 255
        };

    private static readonly IReadOnlyDictionary<string, ulong> Keywords =
        new Dictionary<string, ulong>(StringComparer.Ordinal) {
            ["win:AnyKeyword"] = 0,
            ["win:ResponseTime"] = 0x0001000000000000,
            ["win:WDIContext"] = 0x0002000000000000,
            ["win:WDIDiag"] = 0x0004000000000000,
            ["win:SQM"] = 0x0008000000000000,
            ["win:AuditFailure"] = 0x0010000000000000,
            ["win:AuditSuccess"] = 0x0020000000000000,
            ["win:CorrelationHint"] = 0x0040000000000000,
            ["win:EventlogClassic"] = 0x0080000000000000,
            ["win:ReservedKeyword56"] = 0x0100000000000000,
            ["win:ReservedKeyword57"] = 0x0200000000000000,
            ["win:ReservedKeyword58"] = 0x0400000000000000,
            ["win:ReservedKeyword59"] = 0x0800000000000000,
            ["win:ReservedKeyword60"] = 0x1000000000000000,
            ["win:ReservedKeyword61"] = 0x2000000000000000,
            ["win:ReservedKeyword62"] = 0x4000000000000000,
            ["win:ReservedKeyword63"] = 0x8000000000000000
        };

    internal static byte Level(string name) {
        return Levels[name];
    }

    internal static byte Opcode(string name) {
        return Opcodes[name];
    }

    internal static ulong Keyword(string name) {
        return Keywords[name];
    }

    internal static uint LevelMessageId(string name) {
        return 0x50000000U | Level(name);
    }

    internal static uint OpcodeMessageId(string name) {
        byte value = Opcode(name);
        return value >= 241
            ? uint.MaxValue
            : 0x30000000U | value;
    }

    internal static uint KeywordMessageId(string name) {
        if (name.StartsWith(
                "win:ReservedKeyword",
                StringComparison.Ordinal)) {
            return uint.MaxValue;
        }
        ulong mask = Keyword(name);
        if (mask == 0) {
            return 0x10000000U;
        }
        uint bit = 0;
        while ((mask >>= 1) != 0) {
            bit++;
        }
        return 0x10000001U + bit;
    }
}
