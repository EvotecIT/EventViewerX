namespace EventViewerX;

internal static class EventEnum {
    internal static TEnum[] GetValues<TEnum>() where TEnum : struct, Enum {
#if NET8_0_OR_GREATER
        return Enum.GetValues<TEnum>();
#else
        return (TEnum[])Enum.GetValues(typeof(TEnum));
#endif
    }
}
