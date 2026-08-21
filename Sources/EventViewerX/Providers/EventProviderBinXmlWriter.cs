namespace EventViewerX.Providers;

/// <summary>
/// Writes the inline-name BinXml subset used by manifest EventData templates.
/// </summary>
internal static class EventProviderBinXmlWriter {
    private const byte FragmentHeaderToken = 0x0f;
    private const byte OpenStartElementToken = 0x01;
    private const byte OpenStartElementWithAttributesToken = 0x41;
    private const byte CloseStartElementToken = 0x02;
    private const byte EndElementToken = 0x04;
    private const byte ValueTextToken = 0x05;
    private const byte LastAttributeToken = 0x06;
    private const byte NormalSubstitutionToken = 0x0d;
    private const byte UnicodeStringValueType = 0x01;

    internal static byte[] Write(
        IReadOnlyList<EventProviderFieldDefinition> fields) {

        using var output = new EventProviderBinaryBuffer();
        output.WriteByte(FragmentHeaderToken);
        output.WriteByte(1);
        output.WriteByte(1);
        output.WriteByte(0);

        output.WriteByte(OpenStartElementToken);
        output.WriteUInt16(ushort.MaxValue);
        int eventDataSize = output.ReserveUInt32();
        WriteName(output, "EventData");
        output.WriteByte(CloseStartElementToken);

        for (int index = 0; index < fields.Count; index++) {
            EventProviderFieldDefinition field = fields[index];
            output.WriteByte(OpenStartElementWithAttributesToken);
            output.WriteUInt16(ushort.MaxValue);
            int dataSize = output.ReserveUInt32();
            WriteName(output, "Data");

            int attributesSize = output.ReserveUInt32();
            int attributesStart = output.Position;
            output.WriteByte(LastAttributeToken);
            WriteName(output, "Name");
            output.WriteByte(ValueTextToken);
            output.WriteByte(UnicodeStringValueType);
            output.WriteUInt16(checked((ushort)field.Name.Length));
            output.WriteUtf16(field.Name, nullTerminate: false);
            output.PatchUInt32(
                attributesSize,
                checked((uint)(output.Position - attributesStart)));

            output.WriteByte(CloseStartElementToken);
            output.WriteByte(NormalSubstitutionToken);
            output.WriteUInt16(checked((ushort)index));
            byte inputType = EventProviderBinaryTypes.Input(field.Type);
            if (!string.IsNullOrWhiteSpace(field.Count)) {
                inputType |= 0x80;
            }
            output.WriteByte(inputType);
            output.WriteByte(EndElementToken);
            output.PatchUInt32(
                dataSize,
                checked((uint)(output.Position - dataSize - 4)));
        }

        output.WriteByte(EndElementToken);
        output.WriteByte(0);
        output.PatchUInt32(
            eventDataSize,
            checked((uint)(output.Position - eventDataSize - 4)));
        return output.ToArray();
    }

    internal static ushort NameHash(string value) {
        uint hash = 0;
        foreach (char character in value) {
            hash = unchecked(hash * 65599 + character);
        }
        return unchecked((ushort)hash);
    }

    private static void WriteName(
        EventProviderBinaryBuffer output,
        string value) {

        output.WriteUInt16(NameHash(value));
        output.WriteUInt16(checked((ushort)value.Length));
        output.WriteUtf16(value, nullTerminate: true);
    }
}
