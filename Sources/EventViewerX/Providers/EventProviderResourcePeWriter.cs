using System.Globalization;

namespace EventViewerX.Providers;

/// <summary>
/// Produces a deterministic, code-free PE32+ DLL containing only a Win32
/// resource section.
/// </summary>
internal static class EventProviderResourcePeWriter {
    private const uint ResourceVirtualAddress = 0x1000;
    private const uint SectionAlignment = 0x1000;
    private const uint FileAlignment = 0x200;
    private const uint HeadersSize = 0x200;
    private const uint DirectoryFlag = 0x80000000;

    internal static byte[] Write(
        IReadOnlyDictionary<string, byte[]> messageTables,
        string defaultCulture,
        byte[] template) {

        if (messageTables.Count == 0) {
            throw new ArgumentException(
                "At least one localized message table is required.",
                nameof(messageTables));
        }
        if (template == null || template.Length == 0) {
            throw new ArgumentException(
                "A compiled event template is required.",
                nameof(template));
        }

        List<LocalizedData> messages = messageTables
            .Select(pair => new LocalizedData(
                LanguageId(pair.Key),
                pair.Value))
            .OrderBy(static item => item.Language)
            .ToList();
        if (messages.Select(static item => item.Language)
            .Distinct()
            .Count() != messages.Count) {
            throw new InvalidDataException(
                "Provider cultures resolve to duplicate Windows language identifiers.");
        }
        var templateData = new LocalizedData(
            LanguageId(defaultCulture),
            template);
        byte[] resources = WriteResourceSection(messages, templateData);
        return WritePe(resources);
    }

    private static byte[] WriteResourceSection(
        IReadOnlyList<LocalizedData> messages,
        LocalizedData template) {

        using var output = new EventProviderBinaryBuffer();
        int root = WriteDirectoryHeader(output, named: 1, ids: 1);
        int rootTemplateName = output.ReserveUInt32();
        int rootTemplateDirectory = output.ReserveUInt32();
        output.WriteUInt32(11);
        int rootMessagesDirectory = output.ReserveUInt32();

        int templateType = WriteDirectoryHeader(output, named: 0, ids: 1);
        output.WriteUInt32(1);
        int templateNameDirectory = output.ReserveUInt32();

        int messagesType = WriteDirectoryHeader(output, named: 0, ids: 1);
        output.WriteUInt32(1);
        int messagesNameDirectory = output.ReserveUInt32();

        int templateName = WriteDirectoryHeader(output, named: 0, ids: 1);
        output.WriteUInt32(template.Language);
        int templateDataEntry = output.ReserveUInt32();

        int messagesName = WriteDirectoryHeader(
            output,
            named: 0,
            ids: checked((ushort)messages.Count));
        var messageDataEntries = new List<int>(messages.Count);
        foreach (LocalizedData message in messages) {
            output.WriteUInt32(message.Language);
            messageDataEntries.Add(output.ReserveUInt32());
        }

        int templateTypeName = output.Position;
        const string templateResourceType = "WEVT_TEMPLATE";
        output.WriteUInt16((ushort)templateResourceType.Length);
        output.WriteUtf16(templateResourceType, nullTerminate: false);
        output.Align(4);

        output.PatchUInt32(
            rootTemplateName,
            DirectoryFlag | checked((uint)templateTypeName));
        output.PatchUInt32(
            rootTemplateDirectory,
            DirectoryFlag | checked((uint)templateType));
        output.PatchUInt32(
            rootMessagesDirectory,
            DirectoryFlag | checked((uint)messagesType));
        output.PatchUInt32(
            templateNameDirectory,
            DirectoryFlag | checked((uint)templateName));
        output.PatchUInt32(
            messagesNameDirectory,
            DirectoryFlag | checked((uint)messagesName));

        int templateDataRecord = WriteDataEntry(output, template.Data.Length);
        output.PatchUInt32(
            templateDataEntry,
            checked((uint)templateDataRecord));
        var messageDataRecords = new List<int>(messages.Count);
        foreach (LocalizedData message in messages) {
            messageDataRecords.Add(
                WriteDataEntry(output, message.Data.Length));
        }
        for (int index = 0; index < messages.Count; index++) {
            output.PatchUInt32(
                messageDataEntries[index],
                checked((uint)messageDataRecords[index]));
        }

        output.Align(4);
        PatchAndWriteData(output, templateDataRecord, template.Data);
        for (int index = 0; index < messages.Count; index++) {
            PatchAndWriteData(
                output,
                messageDataRecords[index],
                messages[index].Data);
        }
        _ = root;
        return output.ToArray();
    }

    private static int WriteDirectoryHeader(
        EventProviderBinaryBuffer output,
        ushort named,
        ushort ids) {

        int offset = output.Position;
        output.WriteUInt32(0);
        output.WriteUInt32(0);
        output.WriteUInt16(0);
        output.WriteUInt16(0);
        output.WriteUInt16(named);
        output.WriteUInt16(ids);
        return offset;
    }

    private static int WriteDataEntry(
        EventProviderBinaryBuffer output,
        int size) {

        int offset = output.Position;
        output.WriteUInt32(0);
        output.WriteUInt32(checked((uint)size));
        output.WriteUInt32(0);
        output.WriteUInt32(0);
        return offset;
    }

    private static void PatchAndWriteData(
        EventProviderBinaryBuffer output,
        int dataEntry,
        byte[] data) {

        output.Align(4);
        output.PatchUInt32(
            dataEntry,
            checked(ResourceVirtualAddress + (uint)output.Position));
        output.WriteBytes(data);
    }

    private static byte[] WritePe(byte[] resources) {
        uint rawSize = Align(checked((uint)resources.Length), FileAlignment);
        uint imageSize = Align(
            checked(ResourceVirtualAddress + (uint)resources.Length),
            SectionAlignment);
        using var output = new EventProviderBinaryBuffer();
        output.WriteAscii("MZ");
        while (output.Position < 0x3c) {
            output.WriteByte(0);
        }
        output.WriteUInt32(0x80);
        while (output.Position < 0x80) {
            output.WriteByte(0);
        }

        output.WriteAscii("PE\0\0");
        output.WriteUInt16(0x8664);
        output.WriteUInt16(1);
        output.WriteUInt32(0);
        output.WriteUInt32(0);
        output.WriteUInt32(0);
        output.WriteUInt16(0x00f0);
        output.WriteUInt16(0x2022);

        output.WriteUInt16(0x020b);
        output.WriteByte(14);
        output.WriteByte(0);
        output.WriteUInt32(0);
        output.WriteUInt32(rawSize);
        output.WriteUInt32(0);
        output.WriteUInt32(0);
        output.WriteUInt32(0);
        output.WriteUInt64(0x0000000180000000);
        output.WriteUInt32(SectionAlignment);
        output.WriteUInt32(FileAlignment);
        output.WriteUInt16(6);
        output.WriteUInt16(0);
        output.WriteUInt16(0);
        output.WriteUInt16(0);
        output.WriteUInt16(6);
        output.WriteUInt16(0);
        output.WriteUInt32(0);
        output.WriteUInt32(imageSize);
        output.WriteUInt32(HeadersSize);
        output.WriteUInt32(0);
        output.WriteUInt16(3);
        output.WriteUInt16(0x0160);
        output.WriteUInt64(0x00100000);
        output.WriteUInt64(0x00001000);
        output.WriteUInt64(0x00100000);
        output.WriteUInt64(0x00001000);
        output.WriteUInt32(0);
        output.WriteUInt32(16);
        for (int index = 0; index < 16; index++) {
            if (index == 2) {
                output.WriteUInt32(ResourceVirtualAddress);
                output.WriteUInt32(checked((uint)resources.Length));
            } else {
                output.WriteUInt32(0);
                output.WriteUInt32(0);
            }
        }

        output.WriteBytes(new byte[] {
            (byte)'.', (byte)'r', (byte)'s', (byte)'r',
            (byte)'c', 0, 0, 0
        });
        output.WriteUInt32(checked((uint)resources.Length));
        output.WriteUInt32(ResourceVirtualAddress);
        output.WriteUInt32(rawSize);
        output.WriteUInt32(HeadersSize);
        output.WriteUInt32(0);
        output.WriteUInt32(0);
        output.WriteUInt16(0);
        output.WriteUInt16(0);
        output.WriteUInt32(0x40000040);
        while (output.Position < HeadersSize) {
            output.WriteByte(0);
        }
        output.WriteBytes(resources);
        while (output.Position < HeadersSize + rawSize) {
            output.WriteByte(0);
        }
        return output.ToArray();
    }

    private static ushort LanguageId(string culture) {
        int lcid = CultureInfo.GetCultureInfo(culture).LCID;
        if (lcid < 0 || lcid > ushort.MaxValue) {
            throw new InvalidDataException(
                $"Culture '{culture}' has unsupported Windows LCID {lcid}.");
        }
        return checked((ushort)lcid);
    }

    private static uint Align(uint value, uint alignment) {
        return checked((value + alignment - 1) & ~(alignment - 1));
    }

    private sealed class LocalizedData {
        internal LocalizedData(ushort language, byte[] data) {
            Language = language;
            Data = data;
        }

        internal ushort Language { get; }
        internal byte[] Data { get; }
    }
}
