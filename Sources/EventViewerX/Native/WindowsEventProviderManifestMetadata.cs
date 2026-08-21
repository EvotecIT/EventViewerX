using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EventViewerX.Native;

internal static class WindowsEventProviderManifestMetadata {
    internal static byte GetChannelId(
        string providerName,
        long eventId,
        byte version) {

        IReadOnlyDictionary<long, byte> channels =
            GetChannelIds(providerName);
        long key = CreateKey(eventId, version);
        if (channels.TryGetValue(key, out byte channelId)) {
            return channelId;
        }
        throw new InvalidOperationException(
            $"Provider '{providerName}' did not expose native descriptor metadata for event {eventId} version {version}.");
    }

    internal static IReadOnlyDictionary<long, byte> GetChannelIds(
        string providerName) {

        using WindowsEventNativeMethods.EventHandle metadata =
            WindowsEventNativeMethods.EvtOpenPublisherMetadata(
                IntPtr.Zero,
                providerName,
                null,
                0,
                0);
        if (metadata.IsInvalid) {
            throw CreateException(
                $"Failed to open manifest metadata for provider '{providerName}'.");
        }

        using WindowsEventNativeMethods.EventHandle events =
            WindowsEventNativeMethods.EvtOpenEventMetadataEnum(
                metadata,
                0);
        if (events.IsInvalid) {
            throw CreateException(
                $"Failed to enumerate manifest events for provider '{providerName}'.");
        }

        var channels = new Dictionary<long, byte>();
        while (true) {
            using WindowsEventNativeMethods.EventHandle eventMetadata =
                WindowsEventNativeMethods.EvtNextEventMetadata(
                    events,
                    0);
            if (eventMetadata.IsInvalid) {
                int error = Marshal.GetLastWin32Error();
                if (error ==
                    WindowsEventNativeMethods.ErrorNoMoreItems) {
                    break;
                }
                throw new Win32Exception(
                    error,
                    $"Failed to read manifest events for provider '{providerName}'.");
            }

            long candidateId = checked((long)ReadUnsigned(
                eventMetadata,
                WindowsEventNativeMethods
                    .EventMetadataPropertyId.EventId));
            byte candidateVersion = checked((byte)ReadUnsigned(
                eventMetadata,
                WindowsEventNativeMethods
                    .EventMetadataPropertyId.Version));
            byte channelId = checked((byte)ReadUnsigned(
                eventMetadata,
                WindowsEventNativeMethods
                    .EventMetadataPropertyId.Channel));
            channels[CreateKey(
                candidateId,
                candidateVersion)] = channelId;
        }

        return channels;
    }

    private static ulong ReadUnsigned(
        WindowsEventNativeMethods.EventHandle eventMetadata,
        WindowsEventNativeMethods.EventMetadataPropertyId propertyId) {

        int size = Marshal.SizeOf<
            WindowsEventNativeMethods.EventVariant>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try {
            if (!WindowsEventNativeMethods
                .EvtGetEventMetadataProperty(
                    eventMetadata,
                    propertyId,
                    0,
                    size,
                    buffer,
                    out _)) {

                throw CreateException(
                    $"Failed to read event metadata property '{propertyId}'.");
            }

            WindowsEventNativeMethods.EventVariant value =
                Marshal.PtrToStructure<
                    WindowsEventNativeMethods.EventVariant>(
                    buffer);
            return value.ScalarType switch {
                WindowsEventNativeMethods.VariantType.Byte =>
                    value.ByteValue,
                WindowsEventNativeMethods.VariantType.UInt16 =>
                    value.UInt16Value,
                WindowsEventNativeMethods.VariantType.UInt32 =>
                    value.UInt32Value,
                WindowsEventNativeMethods.VariantType.UInt64 =>
                    value.UInt64Value,
                WindowsEventNativeMethods.VariantType.Int16 =>
                    checked((ulong)value.Int16Value),
                WindowsEventNativeMethods.VariantType.Int32 =>
                    checked((ulong)value.Int32Value),
                WindowsEventNativeMethods.VariantType.Int64 =>
                    checked((ulong)value.Int64Value),
                _ => throw new InvalidOperationException(
                    $"Event metadata property '{propertyId}' returned unsupported native type '{value.ScalarType}'.")
            };
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static Win32Exception CreateException(
        string message) {

        return new Win32Exception(
            Marshal.GetLastWin32Error(),
            message);
    }

    internal static long CreateKey(
        long eventId,
        byte version) {

        return checked((eventId << 8) |
                       version);
    }
}
