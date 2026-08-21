using System.Runtime.InteropServices;

namespace EventViewerX.Native;

internal sealed class RegisteredManifestEventProvider :
    IManifestEventProvider,
    IDisposable {

    private readonly Guid _providerId;
    private readonly ReaderWriterLockSlim _lifetimeLock =
        new(LockRecursionPolicy.NoRecursion);
    private ulong _registrationHandle;
    private bool _disposed;

    internal RegisteredManifestEventProvider(Guid providerId) {
        _providerId = providerId;
        Guid registrationId = providerId;
        uint status = EventRegister(
            ref registrationId,
            IntPtr.Zero,
            IntPtr.Zero,
            out _registrationHandle);
        if (status != 0) {
            throw new System.ComponentModel.Win32Exception(
                checked((int)status),
                $"Windows could not register event provider '{providerId}'.");
        }
    }

    public uint Write(
        ManifestEventDefinition definition,
        IReadOnlyList<object?> payload) {

        _lifetimeLock.EnterReadLock();
        try {
            if (_disposed) {
                throw new ObjectDisposedException(
                    nameof(RegisteredManifestEventProvider));
            }
            if (definition.ProviderId != _providerId) {
                throw new ArgumentException(
                    "The event definition belongs to a different provider.",
                    nameof(definition));
            }
            var descriptor =
                new WindowsManifestEventProvider.EventDescriptor {
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
                _registrationHandle,
                ref descriptor,
                checked((uint)buffer.Descriptors.Length),
                buffer.Descriptors.Length == 0
                    ? null
                    : buffer.Descriptors);
        } finally {
            _lifetimeLock.ExitReadLock();
        }
    }

    public void Dispose() {
        _lifetimeLock.EnterWriteLock();
        try {
            if (_disposed) {
                return;
            }
            _disposed = true;
            if (_registrationHandle != 0) {
                EventUnregister(_registrationHandle);
                _registrationHandle = 0;
            }
        } finally {
            _lifetimeLock.ExitWriteLock();
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
        ref WindowsManifestEventProvider.EventDescriptor eventDescriptor,
        uint userDataCount,
        [In] WindowsManifestEventProvider.EventDataDescriptor[]? userData);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint EventUnregister(
        ulong registrationHandle);
}
