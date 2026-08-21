using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EventViewerX.Native;

internal static class WindowsEventNativeMethods {
    internal const int ErrorInsufficientBuffer = 122;
    internal const int ErrorNoMoreItems = 259;
    internal const int ErrorCancelled = 1223;
    internal const int ErrorTimeout = 1460;
    internal const int ErrorEvtPublisherMetadataNotFound = 15002;
    internal const int ErrorEvtMessageNotFound = 15027;
    internal const int ErrorEvtMessageIdNotFound = 15028;
    internal const int ErrorEvtMessageLocaleNotFound = 15033;
    internal const int SystemPropertyCount = 18;

    [Flags]
    internal enum QueryFlags : uint {
        ChannelPath = 0x1,
        FilePath = 0x2,
        ForwardDirection = 0x100,
        ReverseDirection = 0x200,
        TolerateQueryErrors = 0x1000
    }

    [Flags]
    internal enum ExportLogFlags : uint {
        ChannelPath = 0x1,
        FilePath = 0x2,
        TolerateQueryErrors = 0x1000,
        Overwrite = 0x2000
    }

    internal enum OpenLogFlags : uint {
        ChannelPath = 1,
        FilePath = 2
    }

    internal enum QueryPropertyId {
        Names = 0,
        Statuses = 1
    }

    internal enum LogPropertyId {
        CreationTime = 0,
        LastAccessTime = 1,
        LastWriteTime = 2,
        FileSize = 3,
        Attributes = 4,
        NumberOfLogRecords = 5,
        OldestRecordNumber = 6,
        Full = 7
    }

    internal enum RenderContextFlags : uint {
        System = 1,
        User = 2
    }

    internal enum RenderFlags : uint {
        EventValues = 0,
        EventXml = 1,
        Bookmark = 2
    }

    internal enum FormatMessageFlags : uint {
        Event = 1,
        Level = 2,
        Task = 3,
        Opcode = 4,
        Keyword = 5
    }

    internal enum EventMetadataPropertyId {
        EventId = 0,
        Version = 1,
        Channel = 2,
        Level = 3,
        Opcode = 4,
        Task = 5,
        Keyword = 6,
        MessageId = 7,
        Template = 8
    }

    internal enum LoginClass {
        RpcLogin = 1
    }

    [Flags]
    internal enum SeekFlags : uint {
        RelativeToFirst = 1,
        RelativeToLast = 2,
        RelativeToCurrent = 3,
        RelativeToBookmark = 4,
        OriginMask = 7,
        Strict = 0x10000
    }

    [Flags]
    internal enum SubscribeFlags : uint {
        ToFutureEvents = 1,
        StartAtOldestRecord = 2,
        StartAfterBookmark = 3,
        OriginMask = 3,
        TolerateQueryErrors = 0x1000,
        Strict = 0x10000
    }

    internal enum SubscribeAction : uint {
        Error = 0,
        Deliver = 1
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int SubscribeCallback(
        SubscribeAction action,
        IntPtr userContext,
        IntPtr eventHandle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct RpcLogin {
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string Server;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string? User;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string? Domain;

        internal IntPtr Password;
        internal int Flags;
    }

    internal enum VariantType : uint {
        Null = 0,
        String = 1,
        AnsiString = 2,
        SByte = 3,
        Byte = 4,
        Int16 = 5,
        UInt16 = 6,
        Int32 = 7,
        UInt32 = 8,
        Int64 = 9,
        UInt64 = 10,
        Single = 11,
        Double = 12,
        Boolean = 13,
        Binary = 14,
        Guid = 15,
        SizeT = 16,
        FileTime = 17,
        SystemTime = 18,
        Sid = 19,
        HexInt32 = 20,
        HexInt64 = 21,
        EventHandle = 32,
        Xml = 35
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct EventVariant {
        [FieldOffset(0)]
        internal IntPtr PointerValue;

        [FieldOffset(0)]
        internal byte ByteValue;

        [FieldOffset(0)]
        internal sbyte SByteValue;

        [FieldOffset(0)]
        internal short Int16Value;

        [FieldOffset(0)]
        internal ushort UInt16Value;

        [FieldOffset(0)]
        internal int Int32Value;

        [FieldOffset(0)]
        internal uint UInt32Value;

        [FieldOffset(0)]
        internal long Int64Value;

        [FieldOffset(0)]
        internal ulong UInt64Value;

        [FieldOffset(0)]
        internal float SingleValue;

        [FieldOffset(0)]
        internal double DoubleValue;

        [FieldOffset(8)]
        internal uint Count;

        [FieldOffset(12)]
        internal uint Type;

        internal VariantType ScalarType => (VariantType)(Type & 0x7f);
        internal bool IsArray => (Type & 0x80) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemTime {
        internal ushort Year;
        internal ushort Month;
        internal ushort DayOfWeek;
        internal ushort Day;
        internal ushort Hour;
        internal ushort Minute;
        internal ushort Second;
        internal ushort Milliseconds;
    }

    internal sealed class EventHandle : SafeHandleZeroOrMinusOneIsInvalid {
        internal EventHandle()
            : base(true) {
        }

        protected override bool ReleaseHandle() {
            return EvtClose(handle);
        }
    }

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern EventHandle EvtQuery(
        IntPtr session,
        string? path,
        string? query,
        QueryFlags flags);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtGetQueryInfo(
        EventHandle queryOrSubscription,
        QueryPropertyId propertyId,
        int propertyValueBufferSize,
        IntPtr propertyValueBuffer,
        out int propertyValueBufferUsed);

    [DllImport("wevtapi.dll", SetLastError = true)]
    internal static extern EventHandle EvtOpenSession(
        LoginClass loginClass,
        ref RpcLogin login,
        int timeout,
        int flags);

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtExportLog(
        IntPtr session,
        string? path,
        string? query,
        string targetFilePath,
        ExportLogFlags flags);

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtArchiveExportedLog(
        IntPtr session,
        string logFilePath,
        int locale,
        int flags);

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtClearLog(
        IntPtr session,
        string channelPath,
        string? targetFilePath,
        int flags);

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern EventHandle EvtOpenLog(
        IntPtr session,
        string path,
        OpenLogFlags flags);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtGetLogInfo(
        EventHandle log,
        LogPropertyId propertyId,
        int propertyValueBufferSize,
        IntPtr propertyValueBuffer,
        out int propertyValueBufferUsed);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtNext(
        EventHandle resultSet,
        int eventArraySize,
        [Out] IntPtr[] eventArray,
        int timeout,
        int flags,
        out int returned);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtSeek(
        EventHandle resultSet,
        long position,
        EventHandle? bookmark,
        int timeout,
        SeekFlags flags);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtCancel(EventHandle? objectHandle);

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern EventHandle EvtSubscribe(
        IntPtr session,
        IntPtr signalEvent,
        string? channelPath,
        string? query,
        IntPtr bookmark,
        IntPtr context,
        SubscribeCallback? callback,
        SubscribeFlags flags);

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern EventHandle EvtCreateRenderContext(
        int valuePathsCount,
        string[]? valuePaths,
        RenderContextFlags flags);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtRender(
        EventHandle? context,
        IntPtr fragment,
        RenderFlags flags,
        int bufferSize,
        IntPtr buffer,
        out int bufferUsed,
        out int propertyCount);

    [DllImport("wevtapi.dll", EntryPoint = "EvtRender", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtRenderRaw(
        IntPtr context,
        IntPtr fragment,
        RenderFlags flags,
        int bufferSize,
        IntPtr buffer,
        out int bufferUsed,
        out int propertyCount);

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern EventHandle EvtOpenPublisherMetadata(
        IntPtr session,
        string publisherIdentity,
        string? logFilePath,
        int locale,
        int flags);

    [DllImport("wevtapi.dll", SetLastError = true)]
    internal static extern EventHandle EvtOpenEventMetadataEnum(
        EventHandle publisherMetadata,
        int flags);

    [DllImport("wevtapi.dll", SetLastError = true)]
    internal static extern EventHandle EvtNextEventMetadata(
        EventHandle eventMetadataEnum,
        int flags);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtGetEventMetadataProperty(
        EventHandle eventMetadata,
        EventMetadataPropertyId propertyId,
        int flags,
        int propertyValueBufferSize,
        IntPtr propertyValueBuffer,
        out int propertyValueBufferUsed);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtFormatMessage(
        IntPtr publisherMetadata,
        IntPtr eventHandle,
        int messageId,
        int valueCount,
        IntPtr values,
        FormatMessageFlags flags,
        int bufferSize,
        IntPtr buffer,
        out int bufferUsed);

    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern EventHandle EvtCreateBookmark(string? bookmarkXml);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtUpdateBookmark(
        EventHandle bookmark,
        IntPtr eventHandle);

    [DllImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EvtClose(IntPtr handle);
}
