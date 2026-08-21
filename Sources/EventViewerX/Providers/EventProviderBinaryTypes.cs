namespace EventViewerX.Providers;

/// <summary>Maps the supported public field model to Windows metadata codes.</summary>
internal static class EventProviderBinaryTypes {
    internal static byte Input(EventProviderFieldType type) {
        return type switch {
            EventProviderFieldType.UnicodeString => 1,
            EventProviderFieldType.AnsiString => 2,
            EventProviderFieldType.Int8 => 3,
            EventProviderFieldType.UInt8 => 4,
            EventProviderFieldType.Int16 => 5,
            EventProviderFieldType.UInt16 => 6,
            EventProviderFieldType.Int32 => 7,
            EventProviderFieldType.UInt32 => 8,
            EventProviderFieldType.Int64 => 9,
            EventProviderFieldType.UInt64 => 10,
            EventProviderFieldType.Float => 11,
            EventProviderFieldType.Double => 12,
            EventProviderFieldType.Boolean => 13,
            EventProviderFieldType.Binary => 14,
            EventProviderFieldType.Guid => 15,
            EventProviderFieldType.Pointer => 16,
            EventProviderFieldType.FileTime => 17,
            EventProviderFieldType.SystemTime => 18,
            EventProviderFieldType.Sid => 19,
            EventProviderFieldType.HexInt32 => 20,
            EventProviderFieldType.HexInt64 => 21,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Unsupported Windows event input type.")
        };
    }

    internal static byte Output(EventProviderFieldDefinition field) {
        string output = EventProviderManifestNames.OutputTypeName(field);
        if (output.Length == 0) {
            output = DefaultOutput(field.Type);
        }
        return output switch {
            "xs:string" => 1,
            "xs:dateTime" => 2,
            "xs:byte" => 3,
            "xs:unsignedByte" => 4,
            "xs:short" => 5,
            "xs:unsignedShort" => 6,
            "xs:int" => 7,
            "xs:unsignedInt" => 8,
            "xs:long" => 9,
            "xs:unsignedLong" => 10,
            "xs:float" => 11,
            "xs:double" => 12,
            "xs:boolean" => 13,
            "xs:GUID" => 14,
            "xs:hexBinary" => 15,
            "win:HexInt8" => 16,
            "win:HexInt16" => 17,
            "win:HexInt32" => 18,
            "win:HexInt64" => 19,
            "win:PID" => 20,
            "win:TID" => 21,
            "win:Port" => 22,
            "win:IPv4" => 23,
            "win:IPv6" => 24,
            "win:SocketAddress" => 25,
            "win:CIMDateTime" => 26,
            "win:ETWTIME" => 27,
            "win:Xml" => 28,
            "win:ErrorCode" => 29,
            "win:Win32Error" => 30,
            "win:NTSTATUS" => 31,
            "win:HResult" => 32,
            "win:DateTimeCultureInsensitive" => 33,
            "win:Json" => 34,
            "win:Utf8" => 35,
            "win:Pkcs7WithTypeInfo" => 36,
            "win:CodePointer" => 37,
            "win:DateTimeUtc" => 38,
            _ => throw new InvalidDataException(
                $"Unsupported Windows event output type '{output}'.")
        };
    }

    private static string DefaultOutput(EventProviderFieldType type) {
        return type switch {
            EventProviderFieldType.UnicodeString or
            EventProviderFieldType.AnsiString or
            EventProviderFieldType.Sid => "xs:string",
            EventProviderFieldType.Int8 => "xs:byte",
            EventProviderFieldType.UInt8 => "xs:unsignedByte",
            EventProviderFieldType.Int16 => "xs:short",
            EventProviderFieldType.UInt16 => "xs:unsignedShort",
            EventProviderFieldType.Int32 => "xs:int",
            EventProviderFieldType.UInt32 => "xs:unsignedInt",
            EventProviderFieldType.Int64 => "xs:long",
            EventProviderFieldType.UInt64 => "xs:unsignedLong",
            EventProviderFieldType.Float => "xs:float",
            EventProviderFieldType.Double => "xs:double",
            EventProviderFieldType.Boolean => "xs:boolean",
            EventProviderFieldType.Binary => "xs:hexBinary",
            EventProviderFieldType.Guid => "xs:GUID",
            EventProviderFieldType.Pointer or
            EventProviderFieldType.HexInt64 => "win:HexInt64",
            EventProviderFieldType.FileTime or
            EventProviderFieldType.SystemTime => "xs:dateTime",
            EventProviderFieldType.HexInt32 => "win:HexInt32",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
