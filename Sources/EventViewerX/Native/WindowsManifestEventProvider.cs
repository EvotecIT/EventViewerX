using System.Runtime.InteropServices;

namespace EventViewerX.Native;

internal sealed class WindowsManifestEventProvider :
    IManifestEventProvider {

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventDescriptor {
        internal ushort Id;
        internal byte Version;
        internal byte Channel;
        internal byte Level;
        internal byte Opcode;
        internal ushort Task;
        internal ulong Keyword;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventDataDescriptor {
        internal ulong Pointer;
        internal uint Size;
        internal uint Reserved;
    }

    public uint Write(
        ManifestEventDefinition definition,
        IReadOnlyList<object?> payload) {

        Guid providerId = definition.ProviderId;
        uint status = EventRegister(
            ref providerId,
            IntPtr.Zero,
            IntPtr.Zero,
            out ulong registrationHandle);
        if (status != 0) {
            return status;
        }

        try {
            var descriptor = new EventDescriptor {
                Id = checked((ushort)definition.Id),
                Version = definition.Version,
                Channel = definition.Channel,
                Level = definition.Level,
                Opcode = definition.Opcode,
                Task = definition.Task,
                Keyword = unchecked((ulong)definition.Keywords)
            };
            using var buffer =
                new ManifestEventPayloadBuffer(definition, payload);
            return EventWrite(
                registrationHandle,
                ref descriptor,
                checked((uint)buffer.Descriptors.Length),
                buffer.Descriptors.Length == 0
                    ? null
                    : buffer.Descriptors);
        } finally {
            EventUnregister(registrationHandle);
        }
    }

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint EventRegister(
        ref Guid providerId,
        IntPtr enableCallback,
        IntPtr callbackContext,
        out ulong registrationHandle);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint EventWrite(
        ulong registrationHandle,
        ref EventDescriptor eventDescriptor,
        uint userDataCount,
        [In] EventDataDescriptor[]? userData);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint EventUnregister(
        ulong registrationHandle);
}
