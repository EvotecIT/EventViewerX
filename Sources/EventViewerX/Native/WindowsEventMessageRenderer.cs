using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Runtime.InteropServices;

namespace EventViewerX.Native;

internal sealed class WindowsEventMessageRenderer : IDisposable {
    private const int MaximumProviderCacheEntries = 256;
    private readonly string? _filePath;
    private readonly IntPtr _session;
    private readonly int _locale;
    private readonly int _fallbackLocale;
    private readonly NativeEventBuffer _messageBuffer;
    private readonly WindowsEventBookmarkRenderer _bookmarkRenderer;
    private readonly BoundedDisposableCache<string, ProviderContext>
        _providers =
            new(
                MaximumProviderCacheEntries,
                StringComparer.OrdinalIgnoreCase);

    internal WindowsEventMessageRenderer(
        IntPtr session,
        string? filePath,
        int locale = 0,
        int fallbackLocale = 0) {

        _session = session;
        _filePath = filePath;
        CultureInfo culture = locale == 0
            ? CultureInfo.CurrentUICulture
            : CultureInfo.GetCultureInfo(locale);
        _locale = culture.LCID;
        _fallbackLocale = fallbackLocale == 0
            ? 0
            : CultureInfo.GetCultureInfo(fallbackLocale).LCID;
        NativeEventBuffer? messageBuffer = null;
        WindowsEventBookmarkRenderer? bookmarkRenderer = null;
        try {
            messageBuffer = new NativeEventBuffer();
            bookmarkRenderer = new WindowsEventBookmarkRenderer();
            _messageBuffer = messageBuffer;
            _bookmarkRenderer = bookmarkRenderer;
        } catch {
            bookmarkRenderer?.Dispose();
            messageBuffer?.Dispose();
            throw;
        }
    }

    internal NativeEventMessage Render(
        IntPtr eventHandle,
        NativeEventMetadata metadata,
        bool includeBookmark = false) {
        ProviderContext provider = GetProvider(metadata.ProviderName);
        FormatResult message = Format(
            provider,
            eventHandle,
            WindowsEventNativeMethods.FormatMessageFlags.Event);
        string level = provider.GetLevel(
            metadata.Level,
            () => Format(provider, eventHandle, WindowsEventNativeMethods.FormatMessageFlags.Level).Text);
        string task = provider.GetTask(
            metadata.Task,
            () => Format(provider, eventHandle, WindowsEventNativeMethods.FormatMessageFlags.Task).Text);
        string opcode = provider.GetOpcode(
            metadata.Task,
            metadata.Opcode,
            () => Format(provider, eventHandle, WindowsEventNativeMethods.FormatMessageFlags.Opcode).Text);
        IReadOnlyList<string> keywords = provider.GetKeywords(
            metadata.Keywords,
            () => SplitKeywords(Format(
                provider,
                eventHandle,
                WindowsEventNativeMethods.FormatMessageFlags.Keyword).Text));

        if (string.IsNullOrEmpty(level)) {
            level = metadata.Level switch {
                1 => "Critical",
                2 => "Error",
                3 => "Warning",
                4 => "Information",
                5 => "Verbose",
                _ => metadata.Level?.ToString() ?? string.Empty
            };
        }

        return new NativeEventMessage(
            metadata,
            message.Text,
            level,
            task,
            opcode,
            keywords,
            includeBookmark ? _bookmarkRenderer.Render(eventHandle) : null,
            message.CultureName,
            GetRenderStatus(provider, message.ErrorCode),
            message.ErrorCode);
    }

    private ProviderContext GetProvider(string providerName) {
        return _providers.GetOrAdd(
            providerName,
            () => CreateProviderContext(
                providerName));
    }

    private ProviderContext CreateProviderContext(
        string providerName) {

        WindowsEventNativeMethods.EventHandle primary =
            OpenProvider(providerName, _locale);
        int primaryError =
            primary.IsInvalid
                ? Marshal.GetLastWin32Error()
                : 0;
        WindowsEventNativeMethods.EventHandle? fallback = null;
        int fallbackError = 0;
        string fallbackCultureName = string.Empty;
        if (_fallbackLocale != 0 &&
            _fallbackLocale != _locale) {
            fallback = OpenProvider(
                providerName,
                _fallbackLocale);
            fallbackError = fallback.IsInvalid
                ? Marshal.GetLastWin32Error()
                : 0;
            fallbackCultureName =
                CultureInfo.GetCultureInfo(
                    _fallbackLocale).Name;
        }

        return new ProviderContext(
            primary,
            primaryError,
            CultureInfo.GetCultureInfo(_locale).Name,
            fallback,
            fallbackError,
            fallbackCultureName);
    }

    private WindowsEventNativeMethods.EventHandle OpenProvider(
        string providerName,
        int locale) {

        WindowsEventNativeMethods.EventHandle handle =
            WindowsEventNativeMethods.EvtOpenPublisherMetadata(
                _session,
                providerName,
                _filePath,
                locale,
                0);
        if (!handle.IsInvalid || _filePath == null) {
            return handle;
        }
        handle.Dispose();
        return WindowsEventNativeMethods.EvtOpenPublisherMetadata(
            _session,
            providerName,
            null,
            locale,
            0);
    }

    private unsafe FormatResult Format(
        ProviderContext provider,
        IntPtr eventHandle,
        WindowsEventNativeMethods.FormatMessageFlags flags) {

        FormatResult result = FormatHandle(
            provider.Handle,
            provider.OpenErrorCode,
            provider.CultureName,
            eventHandle,
            flags);
        if (!ShouldTryFallback(result.ErrorCode) ||
            provider.FallbackHandle == null) {
            return result;
        }

        return FormatHandle(
            provider.FallbackHandle,
            provider.FallbackOpenErrorCode,
            provider.FallbackCultureName,
            eventHandle,
            flags);
    }

    private unsafe FormatResult FormatHandle(
        WindowsEventNativeMethods.EventHandle handle,
        int openErrorCode,
        string cultureName,
        IntPtr eventHandle,
        WindowsEventNativeMethods.FormatMessageFlags flags) {

        if (handle.IsInvalid) {
            return new FormatResult(
                string.Empty,
                openErrorCode,
                cultureName);
        }

        IntPtr publisherHandle = handle.DangerousGetHandle();
        if (!WindowsEventNativeMethods.EvtFormatMessage(
                publisherHandle,
                eventHandle,
                0,
                0,
                IntPtr.Zero,
                flags,
                _messageBuffer.Capacity / sizeof(char),
                _messageBuffer.Pointer,
                out int bufferUsed)) {

            int error = Marshal.GetLastWin32Error();
            if (error != WindowsEventNativeMethods.ErrorInsufficientBuffer) {
                return new FormatResult(
                    string.Empty,
                    error,
                    cultureName);
            }

            _messageBuffer.EnsureCapacity(checked(bufferUsed * sizeof(char)));
            if (!WindowsEventNativeMethods.EvtFormatMessage(
                    publisherHandle,
                    eventHandle,
                    0,
                    0,
                    IntPtr.Zero,
                    flags,
                    _messageBuffer.Capacity / sizeof(char),
                    _messageBuffer.Pointer,
                    out bufferUsed)) {
                return new FormatResult(
                    string.Empty,
                    Marshal.GetLastWin32Error(),
                    cultureName);
            }
        }

        if (bufferUsed <= 0) {
            return new FormatResult(
                string.Empty,
                0,
                cultureName);
        }

        var characters = (char*)_messageBuffer.Pointer;
        while (bufferUsed > 0 &&
               characters[bufferUsed - 1] == '\0') {
            bufferUsed--;
        }
        return new FormatResult(
            bufferUsed == 0
                ? string.Empty
                : new string(characters, 0, bufferUsed),
            0,
            cultureName);
    }

    private static bool ShouldTryFallback(int errorCode) {
        return errorCode ==
                   WindowsEventNativeMethods
                       .ErrorEvtPublisherMetadataNotFound ||
               errorCode ==
                   WindowsEventNativeMethods
                       .ErrorEvtMessageNotFound ||
               errorCode ==
                   WindowsEventNativeMethods
                       .ErrorEvtMessageIdNotFound ||
               errorCode ==
                   WindowsEventNativeMethods
                       .ErrorEvtMessageLocaleNotFound;
    }

    private static EventMessageRenderStatus GetRenderStatus(
        ProviderContext provider,
        int errorCode) {

        if (errorCode == 0) {
            return EventMessageRenderStatus.Rendered;
        }
        if ((provider.Handle.IsInvalid &&
             (provider.FallbackHandle == null ||
              provider.FallbackHandle.IsInvalid)) ||
            errorCode == WindowsEventNativeMethods.ErrorEvtPublisherMetadataNotFound) {
            return EventMessageRenderStatus.ProviderMetadataUnavailable;
        }
        if (errorCode == WindowsEventNativeMethods.ErrorEvtMessageNotFound ||
            errorCode == WindowsEventNativeMethods.ErrorEvtMessageIdNotFound ||
            errorCode == WindowsEventNativeMethods.ErrorEvtMessageLocaleNotFound) {
            return EventMessageRenderStatus.MessageResourceUnavailable;
        }
        return EventMessageRenderStatus.Failed;
    }

    private static IReadOnlyList<string> SplitKeywords(string formattedKeywords) {
        if (string.IsNullOrEmpty(formattedKeywords)) {
            return Array.Empty<string>();
        }

        return formattedKeywords.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public void Dispose() {
        _providers.Dispose();
        _messageBuffer.Dispose();
        _bookmarkRenderer.Dispose();
    }

    private readonly struct FormatResult {
        internal FormatResult(
            string text,
            int errorCode,
            string cultureName) {

            Text = text;
            ErrorCode = errorCode;
            CultureName = cultureName;
        }

        internal string Text { get; }
        internal int ErrorCode { get; }
        internal string CultureName { get; }
    }

    private sealed class ProviderContext : IDisposable {
        private readonly Dictionary<byte, string> _levels = new();
        private readonly Dictionary<int, string> _tasks = new();
        private readonly Dictionary<long, string> _opcodes = new();
        private readonly Dictionary<long, IReadOnlyList<string>> _keywords = new();

        internal ProviderContext(
            WindowsEventNativeMethods.EventHandle handle,
            int openErrorCode,
            string cultureName,
            WindowsEventNativeMethods.EventHandle? fallbackHandle,
            int fallbackOpenErrorCode,
            string fallbackCultureName) {

            Handle = handle;
            OpenErrorCode = openErrorCode;
            CultureName = cultureName;
            FallbackHandle = fallbackHandle;
            FallbackOpenErrorCode = fallbackOpenErrorCode;
            FallbackCultureName = fallbackCultureName;
        }

        internal WindowsEventNativeMethods.EventHandle Handle { get; }
        internal int OpenErrorCode { get; }
        internal string CultureName { get; }
        internal WindowsEventNativeMethods.EventHandle? FallbackHandle {
            get;
        }
        internal int FallbackOpenErrorCode { get; }
        internal string FallbackCultureName { get; }

        internal string GetLevel(byte? value, Func<string> factory) {
            return value.HasValue ? GetOrAdd(_levels, value.Value, factory) : string.Empty;
        }

        internal string GetTask(int? value, Func<string> factory) {
            return value.HasValue ? GetOrAdd(_tasks, value.Value, factory) : string.Empty;
        }

        internal string GetOpcode(int? task, short? opcode, Func<string> factory) {
            if (!opcode.HasValue) {
                return string.Empty;
            }
            long key = ((long)(task ?? 0) << 32) | (uint)(ushort)opcode.Value;
            return GetOrAdd(_opcodes, key, factory);
        }

        internal IReadOnlyList<string> GetKeywords(long? value, Func<IReadOnlyList<string>> factory) {
            return value.HasValue ? GetOrAdd(_keywords, value.Value, factory) : Array.Empty<string>();
        }

        private static TValue GetOrAdd<TKey, TValue>(
            Dictionary<TKey, TValue> cache,
            TKey key,
            Func<TValue> factory)
            where TKey : notnull {

            if (cache.TryGetValue(key, out TValue? value)) {
                return value;
            }
            value = factory();
            cache.Add(key, value);
            return value;
        }

        public void Dispose() {
            FallbackHandle?.Dispose();
            Handle.Dispose();
        }
    }
}
