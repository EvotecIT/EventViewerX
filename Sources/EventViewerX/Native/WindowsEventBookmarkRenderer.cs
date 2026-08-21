using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Reflection;
#if NET472
using System.Reflection.Emit;
#endif
using System.Runtime.InteropServices;

namespace EventViewerX.Native;

internal sealed class WindowsEventBookmarkRenderer : IDisposable {
#if NET472
    private static readonly Func<string, EventBookmark> CreateEventBookmark = CreateBookmarkFactory();
#endif
    private readonly NativeEventBuffer _buffer = new();

    internal EventBookmark? Render(IntPtr eventHandle) {
        using WindowsEventNativeMethods.EventHandle bookmark = WindowsEventNativeMethods.EvtCreateBookmark(null);
        if (bookmark.IsInvalid) {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to create a Windows event bookmark.");
        }
        if (!WindowsEventNativeMethods.EvtUpdateBookmark(bookmark, eventHandle)) {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to update a Windows event bookmark.");
        }

        if (!WindowsEventNativeMethods.EvtRenderRaw(
                IntPtr.Zero,
                bookmark.DangerousGetHandle(),
                WindowsEventNativeMethods.RenderFlags.Bookmark,
                _buffer.Capacity,
                _buffer.Pointer,
                out int bufferUsed,
                out _)) {

            int error = Marshal.GetLastWin32Error();
            if (error != WindowsEventNativeMethods.ErrorInsufficientBuffer) {
                throw new System.ComponentModel.Win32Exception(
                    error,
                    "Failed to render a Windows event bookmark.");
            }

            _buffer.EnsureCapacity(bufferUsed);
            if (!WindowsEventNativeMethods.EvtRenderRaw(
                    IntPtr.Zero,
                    bookmark.DangerousGetHandle(),
                    WindowsEventNativeMethods.RenderFlags.Bookmark,
                    _buffer.Capacity,
                    _buffer.Pointer,
                    out bufferUsed,
                    out _)) {
                throw new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to render a Windows event bookmark.");
            }
        }

        string bookmarkXml = Marshal.PtrToStringUni(_buffer.Pointer) ?? string.Empty;
        if (string.IsNullOrEmpty(bookmarkXml)) {
            throw new InvalidDataException(
                "The Windows Event Log API returned an empty bookmark.");
        }
#if NET472
        EventBookmark eventBookmark = CreateEventBookmark(bookmarkXml);
#else
        EventBookmark eventBookmark = new(bookmarkXml);
#endif
        EventBookmarkXml.Register(eventBookmark, bookmarkXml);
        return eventBookmark;
    }

#if NET472
    private static Func<string, EventBookmark> CreateBookmarkFactory() {
        ConstructorInfo? constructor = typeof(EventBookmark).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(string) },
            modifiers: null);
        if (constructor == null) {
            throw new MissingMethodException(typeof(EventBookmark).FullName, ".ctor(string)");
        }

        var method = new DynamicMethod(
            "CreateEventBookmark",
            typeof(EventBookmark),
            new[] { typeof(string) },
            typeof(WindowsEventBookmarkRenderer),
            skipVisibility: true);
        ILGenerator generator = method.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Newobj, constructor);
        generator.Emit(OpCodes.Ret);
        return (Func<string, EventBookmark>)method.CreateDelegate(typeof(Func<string, EventBookmark>));
    }
#endif

    public void Dispose() {
        _buffer.Dispose();
    }
}
