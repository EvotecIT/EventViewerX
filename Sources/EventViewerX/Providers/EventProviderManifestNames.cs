using System.Globalization;
using System.Xml;

namespace EventViewerX.Providers;

internal static class EventProviderManifestNames {
    internal static string SafeId(string value) {
        return Symbol(value, "Value");
    }

    internal static string EventSymbol(
        EventProviderEventDefinition eventDefinition) {

        return Symbol(
                   eventDefinition.Name,
                   "Event_" + eventDefinition.Id) +
               "_V" +
               eventDefinition.Version.ToString(
                   CultureInfo.InvariantCulture);
    }

    internal static string Symbol(string value, string fallback) {
        string source = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value;
        source ??= "Value";
        var builder = new StringBuilder(source.Length + 1);
        foreach (char character in source) {
            builder.Append(
                IsAsciiLetterOrDigit(character)
                    ? char.ToUpperInvariant(character)
                    : '_');
        }
        if (builder.Length == 0) {
            builder.Append("EVX");
        }
        if (char.IsDigit(builder[0])) {
            builder.Insert(0, '_');
        }
        return builder.ToString();
    }

    internal static bool IsUnqualifiedIdentifier(string value) {
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal)) {
            return false;
        }
        try {
            XmlConvert.VerifyNCName(value);
            return true;
        } catch (XmlException) {
            return false;
        }
    }

    internal static bool IsDeclaredQualifiedName(
        string value) {

        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(
                value,
                value.Trim(),
                StringComparison.Ordinal)) {
            return false;
        }
        string[] parts = value.Split(':');
        if (parts.Length != 2 ||
            (parts[0] != "win" &&
             parts[0] != "xs")) {
            return false;
        }
        return IsUnqualifiedIdentifier(
            parts[1]);
    }

    internal static bool IsSupportedOutputType(
        EventProviderFieldType inputType,
        string outputType) {

        if (!IsDeclaredQualifiedName(outputType)) {
            return false;
        }
        return inputType switch {
            EventProviderFieldType.UnicodeString =>
                IsOneOf(
                    outputType,
                    "xs:string",
                    "win:Xml",
                    "win:Json"),
            EventProviderFieldType.AnsiString =>
                IsOneOf(
                    outputType,
                    "xs:string",
                    "win:Xml",
                    "win:Json",
                    "win:Utf8"),
            EventProviderFieldType.Int8 =>
                IsOneOf(
                    outputType,
                    "xs:byte",
                    "xs:string"),
            EventProviderFieldType.UInt8 =>
                IsOneOf(
                    outputType,
                    "xs:unsignedByte",
                    "win:HexInt8",
                    "xs:string",
                    "xs:boolean"),
            EventProviderFieldType.Int16 =>
                IsOneOf(
                    outputType,
                    "xs:short"),
            EventProviderFieldType.UInt16 =>
                IsOneOf(
                    outputType,
                    "xs:unsignedShort",
                    "win:Port",
                    "win:HexInt16",
                    "xs:string"),
            EventProviderFieldType.Int32 =>
                IsOneOf(
                    outputType,
                    "xs:int",
                    "win:HResult"),
            EventProviderFieldType.UInt32 =>
                IsOneOf(
                    outputType,
                    "xs:unsignedInt",
                    "win:PID",
                    "win:TID",
                    "win:IPv4",
                    "win:ETWTIME",
                    "win:ErrorCode",
                    "win:Win32Error",
                    "win:NTSTATUS",
                    "win:HexInt32",
                    "win:CodePointer"),
            EventProviderFieldType.Int64 =>
                IsOneOf(
                    outputType,
                    "xs:long"),
            EventProviderFieldType.UInt64 =>
                IsOneOf(
                    outputType,
                    "xs:unsignedLong",
                    "win:ETWTIME",
                    "win:HexInt64",
                    "win:CodePointer"),
            EventProviderFieldType.Float =>
                IsOneOf(
                    outputType,
                    "xs:float"),
            EventProviderFieldType.Double =>
                IsOneOf(
                    outputType,
                    "xs:double"),
            EventProviderFieldType.Boolean =>
                IsOneOf(
                    outputType,
                    "xs:boolean"),
            EventProviderFieldType.Binary =>
                IsOneOf(
                    outputType,
                    "xs:hexBinary",
                    "win:IPv6",
                    "win:SocketAddress",
                    "win:Pkcs7WithTypeInfo"),
            EventProviderFieldType.Guid =>
                IsOneOf(
                    outputType,
                    "xs:GUID"),
            EventProviderFieldType.Pointer =>
                IsOneOf(
                    outputType,
                    "win:HexInt64",
                    "win:CodePointer",
                    "xs:long",
                    "xs:unsignedLong"),
            EventProviderFieldType.FileTime or
            EventProviderFieldType.SystemTime =>
                IsOneOf(
                    outputType,
                    "xs:dateTime",
                    "win:DateTimeCultureInsensitive",
                    "win:DateTimeUtc"),
            EventProviderFieldType.Sid =>
                IsOneOf(
                    outputType,
                    "xs:string"),
            EventProviderFieldType.HexInt32 =>
                IsOneOf(
                    outputType,
                    "win:HexInt32",
                    "win:ErrorCode",
                    "win:Win32Error",
                    "win:NTSTATUS",
                    "win:CodePointer"),
            EventProviderFieldType.HexInt64 =>
                IsOneOf(
                    outputType,
                    "win:HexInt64",
                    "win:CodePointer"),
            _ => false
        };
    }

    private static bool IsOneOf(
        string value,
        params string[] supported) {

        foreach (string candidate in supported) {
            if (string.Equals(
                    value,
                    candidate,
                    StringComparison.Ordinal)) {
                return true;
            }
        }
        return false;
    }

    private static bool IsAsciiLetterOrDigit(char value) {
        return value is >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or
            >= '0' and <= '9';
    }

    internal static string TypeName(EventProviderFieldType type) {
        return type switch {
            EventProviderFieldType.Guid => "win:GUID",
            EventProviderFieldType.FileTime => "win:FILETIME",
            EventProviderFieldType.SystemTime => "win:SYSTEMTIME",
            EventProviderFieldType.Sid => "win:SID",
            _ => "win:" + type
        };
    }

    internal static string OutputTypeName(
        EventProviderFieldDefinition field) {

        if (!string.IsNullOrWhiteSpace(field.CustomOutputType)) {
            return field.CustomOutputType.Trim();
        }
        return field.OutputType switch {
            EventProviderFieldOutputType.Default => string.Empty,
            EventProviderFieldOutputType.String => "xs:string",
            EventProviderFieldOutputType.DateTime => "xs:dateTime",
            EventProviderFieldOutputType.CultureInsensitiveDateTime =>
                "win:DateTimeCultureInsensitive",
            EventProviderFieldOutputType.Xml => "win:Xml",
            EventProviderFieldOutputType.Json => "win:Json",
            EventProviderFieldOutputType.Utf8 => "win:Utf8",
            EventProviderFieldOutputType.HResult => "win:HResult",
            EventProviderFieldOutputType.ErrorCode => "win:ErrorCode",
            EventProviderFieldOutputType.NtStatus => "win:NTSTATUS",
            EventProviderFieldOutputType.Pid => "win:PID",
            EventProviderFieldOutputType.Tid => "win:TID",
            EventProviderFieldOutputType.Port => "win:Port",
            EventProviderFieldOutputType.IPv4 => "win:IPv4",
            EventProviderFieldOutputType.IPv6 => "win:IPv6",
            EventProviderFieldOutputType.SocketAddress =>
                "win:SocketAddress",
            EventProviderFieldOutputType.CodePointer => "win:CodePointer",
            _ => throw new ArgumentOutOfRangeException(
                nameof(field),
                field.OutputType,
                "Unsupported event field output type.")
        };
    }

    internal static string Hex(ulong value) {
        return "0x" + value.ToString("X", CultureInfo.InvariantCulture);
    }
}
