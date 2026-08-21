using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EventViewerX.Native;

internal sealed class ManifestEventPayloadBuffer : IDisposable {
    private const int MaximumPayloadBytes = 65482;
    private readonly List<IntPtr> _allocations = new();

    internal ManifestEventPayloadBuffer(
        ManifestEventDefinition definition,
        IReadOnlyList<object?> payload) {

        if (definition.PayloadFields.Count != payload.Count) {
            throw new ArgumentException(
                $"Event {definition.Id} version {definition.Version} expects " +
                $"{definition.PayloadFields.Count} payload value(s), but " +
                $"{payload.Count} were supplied.",
                nameof(payload));
        }
        if (payload.Count > 128) {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                "Windows ETW events support at most 128 payload values.");
        }

        Descriptors = new WindowsManifestEventProvider.EventDataDescriptor[
            payload.Count];
        try {
            for (int i = 0; i < payload.Count; i++) {
                byte[] bytes = Encode(
                    definition.PayloadFields,
                    payload,
                    i,
                    MaximumPayloadBytes - TotalBytes);
                Add(bytes, i);
            }
        } catch {
            Dispose();
            throw;
        }
    }

    internal WindowsManifestEventProvider.EventDataDescriptor[] Descriptors {
        get;
    }

    private int TotalBytes { get; set; }

    public void Dispose() {
        foreach (IntPtr allocation in _allocations) {
            Marshal.FreeHGlobal(allocation);
        }
        _allocations.Clear();
    }

    private void Add(byte[] bytes, int index) {
        if (bytes.Length == 0) {
            Descriptors[index] =
                new WindowsManifestEventProvider.EventDataDescriptor {
                    Pointer = 0,
                    Size = 0
                };
            return;
        }
        if (TotalBytes > MaximumPayloadBytes - bytes.Length) {
            throw new ArgumentOutOfRangeException(
                "payload",
                $"The encoded event payload exceeds {MaximumPayloadBytes} bytes.");
        }

        IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
        _allocations.Add(pointer);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        Descriptors[index] =
            new WindowsManifestEventProvider.EventDataDescriptor {
                Pointer = unchecked((ulong)pointer.ToInt64()),
                Size = checked((uint)bytes.Length)
            };
        TotalBytes += bytes.Length;
    }

    private static byte[] Encode(
        IReadOnlyList<ManifestEventPayloadField> fields,
        IReadOnlyList<object?> payload,
        int index,
        int remainingBytes) {

        ManifestEventPayloadField field = fields[index];
        object? value = payload[index];
        int? count = ResolveDimension(
            field.Count,
            "count",
            field,
            fields,
            payload);
        int? length = ResolveDimension(
            field.Length,
            "length",
            field,
            fields,
            payload);
        string inputType = NormalizeInputType(field.InputType);

        if (inputType == "binary" &&
            !length.HasValue) {
            throw new InvalidOperationException(
                $"Manifest binary payload field '{field.Name}' does not declare a length.");
        }

        if (count.HasValue) {
            return EncodeArray(
                field,
                value,
                inputType,
                count.Value,
                length,
                remainingBytes);
        }

        return EncodeScalar(
            field,
            value,
            inputType,
            length,
            remainingBytes);
    }

    private static byte[] EncodeScalar(
        ManifestEventPayloadField field,
        object? value,
        string inputType,
        int? length,
        int remainingBytes) {

        switch (inputType) {
            case "unicodestring":
                return EncodeUnicodeString(
                    field,
                    value,
                    length,
                    remainingBytes);
            case "ansistring":
                return EncodeAnsiString(
                    field,
                    value,
                    length,
                    remainingBytes);
            case "int8":
                return new[] {
                    unchecked((byte)Convert.ToSByte(
                        RequireValue(field, value),
                        CultureInfo.InvariantCulture))
                };
            case "uint8":
                return new[] {
                    Convert.ToByte(
                        RequireValue(field, value),
                        CultureInfo.InvariantCulture)
                };
            case "int16":
                return BitConverter.GetBytes(Convert.ToInt16(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture));
            case "uint16":
                return BitConverter.GetBytes(Convert.ToUInt16(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture));
            case "int32":
                return BitConverter.GetBytes(Convert.ToInt32(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture));
            case "hexint32":
                return BitConverter.GetBytes(ConvertHexInt32(
                    field,
                    value));
            case "uint32":
                return BitConverter.GetBytes(Convert.ToUInt32(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture));
            case "int64":
                return BitConverter.GetBytes(Convert.ToInt64(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture));
            case "hexint64":
                return BitConverter.GetBytes(ConvertHexInt64(
                    field,
                    value));
            case "uint64":
                return BitConverter.GetBytes(Convert.ToUInt64(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture));
            case "float":
                return BitConverter.GetBytes(Convert.ToSingle(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture));
            case "double":
                return BitConverter.GetBytes(Convert.ToDouble(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture));
            case "boolean":
                return BitConverter.GetBytes(Convert.ToBoolean(
                    RequireValue(field, value),
                    CultureInfo.InvariantCulture) ? 1 : 0);
            case "binary":
                byte[] bytes = EncodeBinary(field, value);
                ValidateEncodedLength(
                    field,
                    bytes.Length,
                    length);
                ValidateFitsPayload(
                    field,
                    bytes.Length,
                    remainingBytes);
                return bytes;
            case "guid":
                return ConvertGuid(field, value).ToByteArray();
            case "pointer":
                return IntPtr.Size == 8
                    ? BitConverter.GetBytes(ConvertPointer64(field, value))
                    : BitConverter.GetBytes(ConvertPointer32(field, value));
            case "filetime":
                return BitConverter.GetBytes(
                    ConvertDateTime(field, value).ToFileTimeUtc());
            case "systemtime":
                return EncodeSystemTime(ConvertDateTime(field, value));
            case "sid":
                return EncodeSid(field, value);
            default:
                throw new NotSupportedException(
                    $"Manifest payload field '{field.Name}' uses unsupported " +
                    $"input type '{field.InputType}'.");
        }
    }

    private static byte[] EncodeArray(
        ManifestEventPayloadField field,
        object? value,
        string inputType,
        int count,
        int? elementLength,
        int remainingBytes) {

        if (count < 0) {
            throw new ArgumentOutOfRangeException(
                field.Name,
                $"Manifest payload field '{field.Name}' has a negative count.");
        }
        if (count > MaximumPayloadBytes) {
            throw new ArgumentOutOfRangeException(
                field.Name,
                $"Manifest payload field '{field.Name}' count {count} exceeds the bounded event payload capacity.");
        }
        if (count == 0 &&
            value == null) {
            return Array.Empty<byte>();
        }
        int minimumElementBytes =
            GetMinimumEncodedSize(
                inputType,
                elementLength);
        if (minimumElementBytes > 0 &&
            count > remainingBytes /
                minimumElementBytes) {
            throw new ArgumentOutOfRangeException(
                field.Name,
                $"Manifest payload field '{field.Name}' cannot encode {count} values within the remaining {remainingBytes}-byte event payload budget.");
        }
        if (inputType == "binary" &&
            value is byte[] binaryValue) {
            if (count == 1) {
                return EncodeScalar(
                    field,
                    binaryValue,
                    inputType,
                    elementLength,
                    remainingBytes);
            }
            if (count == 0 &&
                binaryValue.Length == 0) {
                return Array.Empty<byte>();
            }
            throw new ArgumentException(
                $"Manifest payload field '{field.Name}' requires {count} binary values.",
                field.Name);
        }
        if (value is string ||
            value is not IEnumerable enumerable) {
            if (count == 1) {
                return EncodeScalar(
                    field,
                    value,
                    inputType,
                    elementLength,
                    remainingBytes);
            }
            throw new ArgumentException(
                $"Manifest payload field '{field.Name}' requires {count} values.",
                field.Name);
        }

        var values = new List<object?>();
        foreach (object? item in enumerable) {
            if (values.Count >= count) {
                throw new ArgumentException(
                    $"Manifest payload field '{field.Name}' requires {count} values, but more values were supplied.",
                    field.Name);
            }
            values.Add(item);
        }
        if (values.Count != count) {
            throw new ArgumentException(
                $"Manifest payload field '{field.Name}' requires {count} values, but {values.Count} were supplied.",
                field.Name);
        }

        using var stream = new MemoryStream();
        foreach (object? item in values) {
            byte[] encoded = EncodeScalar(
                field,
                item,
                inputType,
                elementLength,
                remainingBytes -
                checked((int)stream.Length));
            ValidateFitsPayload(
                field,
                encoded.Length,
                remainingBytes -
                checked((int)stream.Length));
            stream.Write(
                encoded,
                0,
                encoded.Length);
        }
        return stream.ToArray();
    }

    private static int? ResolveDimension(
        string expression,
        string attributeName,
        ManifestEventPayloadField field,
        IReadOnlyList<ManifestEventPayloadField> fields,
        IReadOnlyList<object?> payload) {

        if (string.IsNullOrWhiteSpace(expression)) {
            return null;
        }
        string normalized = expression.Trim();
        int value;
        if (!int.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)) {

            int referencedIndex = -1;
            for (int i = 0; i < fields.Count; i++) {
                if (string.Equals(
                        fields[i].Name,
                        normalized,
                        StringComparison.Ordinal)) {
                    referencedIndex = i;
                    break;
                }
            }
            if (referencedIndex < 0) {
                throw new InvalidOperationException(
                    $"Manifest payload field '{field.Name}' {attributeName} references unknown field '{normalized}'.");
            }
            try {
                value = Convert.ToInt32(
                    RequireValue(
                        fields[referencedIndex],
                        payload[referencedIndex]),
                    CultureInfo.InvariantCulture);
            } catch (Exception exception)
                when (exception is FormatException ||
                      exception is InvalidCastException ||
                      exception is OverflowException) {
                throw new ArgumentException(
                    $"Manifest payload field '{field.Name}' {attributeName} reference '{normalized}' must contain a non-negative 32-bit integer.",
                    field.Name,
                    exception);
            }
        }

        if (value < 0) {
            throw new ArgumentOutOfRangeException(
                field.Name,
                $"Manifest payload field '{field.Name}' {attributeName} cannot be negative.");
        }
        return value;
    }

    private static string NormalizeInputType(string inputType) {
        string normalized = inputType?.Trim() ?? string.Empty;
        if (normalized.Length == 0) {
            return "unicodestring";
        }
        int separator = normalized.IndexOf(':');
        if (separator >= 0) {
            normalized = normalized.Substring(separator + 1);
        }
        return normalized.ToLowerInvariant();
    }

    private static object RequireValue(
        ManifestEventPayloadField field,
        object? value) {

        return value ?? throw new ArgumentNullException(
            field.Name,
            $"Manifest payload field '{field.Name}' cannot be null for " +
            $"input type '{field.InputType}'.");
    }

    private static byte[] EncodeUnicodeString(
        ManifestEventPayloadField field,
        object? value,
        int? length,
        int remainingBytes) {

        string text = Convert.ToString(
            value,
            CultureInfo.InvariantCulture) ?? string.Empty;
        if (length.HasValue) {
            int encodedBytes;
            try {
                encodedBytes = checked(
                    length.Value * sizeof(char));
            } catch (OverflowException) {
                throw new ArgumentOutOfRangeException(
                    field.Name,
                    length.Value,
                    $"Manifest payload field '{field.Name}' length is too large.");
            }
            ValidateFitsPayload(
                field,
                encodedBytes,
                remainingBytes);
            return Encoding.Unicode.GetBytes(
                PadFixedLengthString(
                    field,
                    text,
                    length.Value));
        }
        int nullTerminatedBytes;
        try {
            nullTerminatedBytes = checked(
                (text.Length + 1) *
                sizeof(char));
        } catch (OverflowException) {
            throw new ArgumentOutOfRangeException(
                field.Name,
                text.Length,
                $"Manifest payload field '{field.Name}' is too large.");
        }
        ValidateFitsPayload(
            field,
            nullTerminatedBytes,
            remainingBytes);
        return Encoding.Unicode.GetBytes(
            text + '\0');
    }

    private static byte[] EncodeAnsiString(
        ManifestEventPayloadField field,
        object? value,
        int? length,
        int remainingBytes) {

        string text = Convert.ToString(
            value,
            CultureInfo.InvariantCulture) ?? string.Empty;
        int size = WideCharToMultiByte(
            0,
            0,
            text,
            text.Length,
            null,
            0,
            IntPtr.Zero,
            IntPtr.Zero);
        if (size == 0 && text.Length > 0) {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to measure the ANSI event payload.");
        }
        if (length.HasValue) {
            int paddingBytes =
                Math.Max(
                    0,
                    length.Value - text.Length);
            int encodedBytes;
            try {
                encodedBytes = checked(
                    size + paddingBytes);
            } catch (OverflowException) {
                throw new ArgumentOutOfRangeException(
                    field.Name,
                    length.Value,
                    $"Manifest payload field '{field.Name}' length is too large.");
            }
            ValidateFitsPayload(
                field,
                encodedBytes,
                remainingBytes);
            text = PadFixedLengthString(
                field,
                text,
                length.Value);
            size = WideCharToMultiByte(
                0,
                0,
                text,
                text.Length,
                null,
                0,
                IntPtr.Zero,
                IntPtr.Zero);
            if (size == 0 && text.Length > 0) {
                throw new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to measure the fixed-length ANSI event payload.");
            }
        } else {
            ValidateFitsPayload(
                field,
                checked(size + 1),
                remainingBytes);
        }
        byte[] bytes = new byte[
            size + (length.HasValue ? 0 : 1)];
        if (size > 0) {
            int written = WideCharToMultiByte(
                0,
                0,
                text,
                text.Length,
                bytes,
                size,
                IntPtr.Zero,
                IntPtr.Zero);
            if (written != size) {
                throw new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to encode the ANSI event payload.");
            }
        }
        return bytes;
    }

    private static int GetMinimumEncodedSize(
        string inputType,
        int? elementLength) {

        return inputType switch {
            "unicodestring" => elementLength.HasValue
                ? elementLength.Value >
                  MaximumPayloadBytes / sizeof(char)
                    ? MaximumPayloadBytes + 1
                    : elementLength.Value * sizeof(char)
                : sizeof(char),
            "ansistring" => elementLength.HasValue &&
                            elementLength.Value >
                            MaximumPayloadBytes
                ? MaximumPayloadBytes + 1
                : elementLength ?? 1,
            "int8" or "uint8" => 1,
            "int16" or "uint16" => 2,
            "int32" or "uint32" or "hexint32" or
            "float" or "boolean" => 4,
            "int64" or "uint64" or "hexint64" or
            "double" or "filetime" => 8,
            "guid" or "systemtime" => 16,
            "pointer" => IntPtr.Size,
            "binary" => elementLength.HasValue &&
                        elementLength.Value >
                        MaximumPayloadBytes
                ? MaximumPayloadBytes + 1
                : elementLength ?? 0,
            "sid" => 8,
            _ => 0
        };
    }

    private static void ValidateFitsPayload(
        ManifestEventPayloadField field,
        int encodedBytes,
        int remainingBytes) {

        if (encodedBytes < 0 ||
            encodedBytes > remainingBytes) {
            throw new ArgumentOutOfRangeException(
                field.Name,
                $"Manifest payload field '{field.Name}' requires {encodedBytes} encoded bytes, exceeding the remaining {remainingBytes}-byte event payload budget.");
        }
    }

    private static string PadFixedLengthString(
        ManifestEventPayloadField field,
        string value,
        int length) {

        if (length == 0) {
            if (value.Length == 0) {
                return string.Empty;
            }
            throw new ArgumentException(
                $"Manifest payload field '{field.Name}' requires length 0, but a non-empty value was supplied.",
                field.Name);
        }
        int contentLength = length - 1;
        if (value.Length > contentLength) {
            throw new ArgumentException(
                $"Manifest payload field '{field.Name}' allows at most {contentLength} character(s) before its null terminator, but {value.Length} were supplied.",
                field.Name);
        }
        return value.PadRight(contentLength, '\0') + '\0';
    }

    private static void ValidateEncodedLength(
        ManifestEventPayloadField field,
        int actual,
        int? expected) {

        if (expected.HasValue &&
            actual != expected.Value) {
            throw new ArgumentException(
                $"Manifest payload field '{field.Name}' requires length {expected.Value}, but the encoded value has length {actual}.",
                field.Name);
        }
    }

    private static byte[] EncodeBinary(
        ManifestEventPayloadField field,
        object? value) {

        if (value is byte[] bytes) {
            return bytes;
        }
        throw new ArgumentException(
            $"Manifest payload field '{field.Name}' requires a byte array.",
            field.Name);
    }

    private static Guid ConvertGuid(
        ManifestEventPayloadField field,
        object? value) {

        if (value is Guid guid) {
            return guid;
        }
        string text = Convert.ToString(
            RequireValue(field, value),
            CultureInfo.InvariantCulture) ?? string.Empty;
        if (Guid.TryParse(text, out Guid parsed)) {
            return parsed;
        }
        throw new ArgumentException(
            $"Manifest payload field '{field.Name}' requires a GUID.",
            field.Name);
    }

    private static DateTime ConvertDateTime(
        ManifestEventPayloadField field,
        object? value) {

        if (value is DateTime dateTime) {
            return dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.ToUniversalTime();
        }
        if (value is DateTimeOffset offset) {
            return offset.UtcDateTime;
        }
        string text = Convert.ToString(
            RequireValue(field, value),
            CultureInfo.InvariantCulture) ?? string.Empty;
        if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed)) {
            return parsed.UtcDateTime;
        }
        throw new ArgumentException(
            $"Manifest payload field '{field.Name}' requires a date and time.",
            field.Name);
    }

    private static int ConvertHexInt32(
        ManifestEventPayloadField field,
        object? value) {

        if (value is string text) {
            string normalized = RemoveHexPrefix(text);
            if (uint.TryParse(
                    normalized,
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out uint parsed)) {
                return unchecked((int)parsed);
            }
            throw new ArgumentException(
                $"Manifest payload field '{field.Name}' requires a 32-bit " +
                "hexadecimal integer.",
                field.Name);
        }
        return Convert.ToInt32(
            RequireValue(field, value),
            CultureInfo.InvariantCulture);
    }

    private static long ConvertHexInt64(
        ManifestEventPayloadField field,
        object? value) {

        if (value is string text) {
            string normalized = RemoveHexPrefix(text);
            if (ulong.TryParse(
                    normalized,
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out ulong parsed)) {
                return unchecked((long)parsed);
            }
            throw new ArgumentException(
                $"Manifest payload field '{field.Name}' requires a 64-bit " +
                "hexadecimal integer.",
                field.Name);
        }
        return Convert.ToInt64(
            RequireValue(field, value),
            CultureInfo.InvariantCulture);
    }

    private static ulong ConvertPointer64(
        ManifestEventPayloadField field,
        object? value) {

        if (value is IntPtr pointer) {
            return unchecked((ulong)pointer.ToInt64());
        }
        if (value is UIntPtr unsignedPointer) {
            return unsignedPointer.ToUInt64();
        }
        return Convert.ToUInt64(
            RequireValue(field, value),
            CultureInfo.InvariantCulture);
    }

    private static uint ConvertPointer32(
        ManifestEventPayloadField field,
        object? value) {

        if (value is IntPtr pointer) {
            return unchecked((uint)pointer.ToInt32());
        }
        if (value is UIntPtr unsignedPointer) {
            return unsignedPointer.ToUInt32();
        }
        return Convert.ToUInt32(
            RequireValue(field, value),
            CultureInfo.InvariantCulture);
    }

    private static string RemoveHexPrefix(string value) {
        string normalized = value.Trim();
        return normalized.StartsWith(
            "0x",
            StringComparison.OrdinalIgnoreCase)
            ? normalized.Substring(2)
            : normalized;
    }

    private static byte[] EncodeSystemTime(DateTime value) {
        DateTime utc = value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();
        byte[] bytes = new byte[16];
        WriteUInt16(bytes, 0, checked((ushort)utc.Year));
        WriteUInt16(bytes, 2, checked((ushort)utc.Month));
        WriteUInt16(bytes, 4, checked((ushort)utc.DayOfWeek));
        WriteUInt16(bytes, 6, checked((ushort)utc.Day));
        WriteUInt16(bytes, 8, checked((ushort)utc.Hour));
        WriteUInt16(bytes, 10, checked((ushort)utc.Minute));
        WriteUInt16(bytes, 12, checked((ushort)utc.Second));
        WriteUInt16(bytes, 14, checked((ushort)utc.Millisecond));
        return bytes;
    }

    private static byte[] EncodeSid(
        ManifestEventPayloadField field,
        object? value) {

        SecurityIdentifier sid;
        if (value is SecurityIdentifier securityIdentifier) {
            sid = securityIdentifier;
        } else {
            string text = Convert.ToString(
                RequireValue(field, value),
                CultureInfo.InvariantCulture) ?? string.Empty;
            try {
                sid = new SecurityIdentifier(text);
            } catch (ArgumentException exception) {
                throw new ArgumentException(
                    $"Manifest payload field '{field.Name}' requires a SID.",
                    field.Name,
                    exception);
            }
        }
        byte[] bytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static void WriteUInt16(
        byte[] bytes,
        int offset,
        ushort value) {

        byte[] encoded = BitConverter.GetBytes(value);
        bytes[offset] = encoded[0];
        bytes[offset + 1] = encoded[1];
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern int WideCharToMultiByte(
        uint codePage,
        uint flags,
        string wideText,
        int wideCharacterCount,
        byte[]? multiByteText,
        int multiByteCount,
        IntPtr defaultCharacter,
        IntPtr usedDefaultCharacter);
}
