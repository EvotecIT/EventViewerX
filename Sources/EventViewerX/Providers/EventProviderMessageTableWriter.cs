namespace EventViewerX.Providers;

/// <summary>Writes the documented Win32 MESSAGE_RESOURCE_DATA format.</summary>
internal static class EventProviderMessageTableWriter {
    internal static byte[] Write(
        IReadOnlyDictionary<uint, string> messages) {

        if (messages.Count == 0) {
            throw new ArgumentException(
                "At least one message is required.",
                nameof(messages));
        }
        uint[] ids = messages.Keys.OrderBy(static id => id).ToArray();
        List<Block> blocks = CreateBlocks(ids);
        using var output = new EventProviderBinaryBuffer();
        output.WriteUInt32(checked((uint)blocks.Count));
        foreach (Block block in blocks) {
            output.WriteUInt32(block.LowId);
            output.WriteUInt32(block.HighId);
            block.OffsetPatch = output.ReserveUInt32();
        }
        foreach (Block block in blocks) {
            output.PatchUInt32(
                block.OffsetPatch,
                checked((uint)output.Position));
            for (uint id = block.LowId; id <= block.HighId; id++) {
                WriteEntry(output, messages[id]);
                if (id == uint.MaxValue) {
                    break;
                }
            }
        }
        return output.ToArray();
    }

    private static List<Block> CreateBlocks(IReadOnlyList<uint> ids) {
        var blocks = new List<Block>();
        uint low = ids[0];
        uint previous = low;
        for (int index = 1; index < ids.Count; index++) {
            uint id = ids[index];
            if (id != previous + 1) {
                blocks.Add(new Block(low, previous));
                low = id;
            }
            previous = id;
        }
        blocks.Add(new Block(low, previous));
        return blocks;
    }

    private static void WriteEntry(
        EventProviderBinaryBuffer output,
        string message) {

        string normalized = message.TrimEnd('\r', '\n') + "\r\n";
        byte[] text = Encoding.Unicode.GetBytes(normalized + "\0");
        int entryLength = checked(sizeof(ushort) * 2 + text.Length);
        int alignedLength = checked((entryLength + 3) & ~3);
        if (alignedLength > ushort.MaxValue) {
            throw new InvalidDataException(
                "A provider message exceeds the Windows message-table entry limit.");
        }
        output.WriteUInt16((ushort)alignedLength);
        output.WriteUInt16(1);
        output.WriteBytes(text);
        while (entryLength++ < alignedLength) {
            output.WriteByte(0);
        }
    }

    private sealed class Block {
        internal Block(uint lowId, uint highId) {
            LowId = lowId;
            HighId = highId;
        }

        internal uint LowId { get; }
        internal uint HighId { get; }
        internal int OffsetPatch { get; set; }
    }
}
