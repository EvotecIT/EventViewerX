using System;
using System.Collections.Generic;
using System.Globalization;

namespace EventViewerX {
    public partial class EventObject {
        /// <summary>
        /// Attempts to return a message line by index. Negative indices address from the end (for example, <c>-1</c> is the last line).
        /// </summary>
        /// <param name="index">Zero-based line index, or negative index from the end.</param>
        /// <param name="line">Resolved line value when found; otherwise empty string.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned line.</param>
        /// <returns><c>true</c> when the line exists; otherwise <c>false</c>.</returns>
        public bool TryGetMessageLine(int index, out string line, bool trim = true) {
            line = string.Empty;
            IReadOnlyList<string>? messageLines = MessageLines;
            if (messageLines == null || messageLines.Count == 0) {
                return false;
            }

            int effectiveIndex = index < 0 ? messageLines.Count + index : index;
            if (effectiveIndex < 0 || effectiveIndex >= messageLines.Count) {
                return false;
            }

            string value = messageLines[effectiveIndex] ?? string.Empty;
            line = trim ? value.Trim() : value;
            return true;
        }

        /// <summary>
        /// Returns a message line by index or an empty string when missing. Negative indices address from the end.
        /// </summary>
        /// <param name="index">Zero-based line index, or negative index from the end.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned line.</param>
        /// <returns>The requested message line or empty string when out of range.</returns>
        public string GetMessageLineOrEmpty(int index, bool trim = true) {
            return TryGetMessageLine(index, out string line, trim) ? line : string.Empty;
        }

        /// <summary>
        /// Attempts to retrieve an event data value by key.
        /// </summary>
        /// <param name="key">Dictionary key to lookup.</param>
        /// <param name="value">Resolved value when found.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned value.</param>
        /// <returns><c>true</c> when the key exists; otherwise <c>false</c>.</returns>
        public bool TryGetDataValue(string key, out string value, bool trim = true) {
            return TryGetValueFromDictionary(Data, key, out value, trim);
        }

        /// <summary>
        /// Attempts to retrieve an event data value by a canonical known field.
        /// </summary>
        /// <param name="field">Known field to lookup.</param>
        /// <param name="value">Resolved value when found.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned value.</param>
        /// <returns><c>true</c> when the key exists; otherwise <c>false</c>.</returns>
        public bool TryGetDataValue(KnownEventField field, out string value, bool trim = true) {
            return TryGetDataValue(field.ToEventFieldKey(), out value, trim);
        }

        /// <summary>
        /// Returns an event data value by known field or an empty string when missing.
        /// </summary>
        /// <param name="field">Known field to lookup.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned value.</param>
        /// <returns>Resolved value, or empty string when key is missing.</returns>
        public string GetDataValueOrEmpty(KnownEventField field, bool trim = true) {
            return TryGetDataValue(field, out string value, trim) ? value : string.Empty;
        }

        /// <summary>
        /// Returns an event data value by key or an empty string when missing.
        /// </summary>
        /// <param name="key">Dictionary key to lookup.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned value.</param>
        /// <returns>Resolved value, or empty string when key is missing.</returns>
        public string GetDataValueOrEmpty(string key, bool trim = true) {
            return TryGetDataValue(key, out string value, trim) ? value : string.Empty;
        }

        /// <summary>
        /// Attempts to retrieve a message dictionary value by key.
        /// </summary>
        /// <param name="key">Dictionary key to lookup.</param>
        /// <param name="value">Resolved value when found.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned value.</param>
        /// <returns><c>true</c> when the key exists; otherwise <c>false</c>.</returns>
        public bool TryGetMessageValue(string key, out string value, bool trim = true) {
            return TryGetValueFromDictionary(MessageData, key, out value, trim);
        }

        /// <summary>
        /// Attempts to retrieve a message dictionary value by a canonical known field.
        /// </summary>
        /// <param name="field">Known field to lookup.</param>
        /// <param name="value">Resolved value when found.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned value.</param>
        /// <returns><c>true</c> when the key exists; otherwise <c>false</c>.</returns>
        public bool TryGetMessageValue(KnownEventField field, out string value, bool trim = true) {
            return TryGetMessageValue(field.ToEventFieldKey(), out value, trim);
        }

        /// <summary>
        /// Returns a message dictionary value by known field or an empty string when missing.
        /// </summary>
        /// <param name="field">Known field to lookup.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned value.</param>
        /// <returns>Resolved value, or empty string when key is missing.</returns>
        public string GetMessageValueOrEmpty(KnownEventField field, bool trim = true) {
            return TryGetMessageValue(field, out string value, trim) ? value : string.Empty;
        }

        /// <summary>
        /// Returns a message dictionary value by key or an empty string when missing.
        /// </summary>
        /// <param name="key">Dictionary key to lookup.</param>
        /// <param name="trim">When <c>true</c>, trims whitespace from the returned value.</param>
        /// <returns>Resolved value, or empty string when key is missing.</returns>
        public string GetMessageValueOrEmpty(string key, bool trim = true) {
            return TryGetMessageValue(key, out string value, trim) ? value : string.Empty;
        }

        /// <summary>
        /// Attempts to parse a known event data field into a target enum.
        /// </summary>
        /// <typeparam name="TEnum">Target enum type.</typeparam>
        /// <param name="field">Known field to lookup.</param>
        /// <param name="value">Parsed enum value when conversion succeeds.</param>
        /// <param name="numericBase">Preferred numeric base for numeric payloads.</param>
        /// <param name="trimPrefix">Optional literal prefix to remove before parsing (for example <c>%%</c>).</param>
        /// <returns><c>true</c> when conversion succeeds; otherwise <c>false</c>.</returns>
        public bool TryGetDataEnum<TEnum>(
            KnownEventField field,
            out TEnum value,
            EventFieldNumericBase numericBase = EventFieldNumericBase.Auto,
            string trimPrefix = "") where TEnum : struct, Enum {
            value = default;
            if (!TryGetDataValue(field, out string rawValue)) {
                return false;
            }

            return TryParseEnumValue(rawValue, out value, numericBase, trimPrefix);
        }

        /// <summary>
        /// Attempts to parse a known message field into a target enum.
        /// </summary>
        /// <typeparam name="TEnum">Target enum type.</typeparam>
        /// <param name="field">Known field to lookup.</param>
        /// <param name="value">Parsed enum value when conversion succeeds.</param>
        /// <param name="numericBase">Preferred numeric base for numeric payloads.</param>
        /// <param name="trimPrefix">Optional literal prefix to remove before parsing (for example <c>%%</c>).</param>
        /// <returns><c>true</c> when conversion succeeds; otherwise <c>false</c>.</returns>
        public bool TryGetMessageEnum<TEnum>(
            KnownEventField field,
            out TEnum value,
            EventFieldNumericBase numericBase = EventFieldNumericBase.Auto,
            string trimPrefix = "") where TEnum : struct, Enum {
            value = default;
            if (!TryGetMessageValue(field, out string rawValue)) {
                return false;
            }

            return TryParseEnumValue(rawValue, out value, numericBase, trimPrefix);
        }

        /// <summary>
        /// Gets one or two data values by key (case-insensitive) and optionally concatenates them.
        /// </summary>
        /// <param name="key1">Primary key name.</param>
        /// <param name="key2">Secondary key name.</param>
        /// <param name="splitter">Text inserted between values when both keys exist.</param>
        /// <param name="reverseOrder">When <c>true</c>, returns key2+splitter+key1.</param>
        /// <returns>Resolved value, concatenated value, or empty string when keys are missing.</returns>
        public string GetValueFromDataDictionary(string key1, string? key2 = null, string splitter = "\\", bool reverseOrder = false) {
            string secondValue = string.Empty;
            bool hasFirst = TryGetDataValue(key1, out string firstValue, trim: false);
            bool hasSecond = key2 != null && TryGetDataValue(key2, out secondValue, trim: false);

            if (hasFirst && hasSecond) {
                string joiner = splitter ?? string.Empty;
                return reverseOrder
                    ? secondValue + joiner + firstValue
                    : firstValue + joiner + secondValue;
            }

            if (hasFirst) {
                return firstValue;
            }

            return hasSecond ? secondValue : string.Empty;
        }

        /// <summary>
        /// Gets one or two data values by known field (case-insensitive) and optionally concatenates them.
        /// </summary>
        /// <param name="key1">Primary known field.</param>
        /// <param name="key2">Secondary known field.</param>
        /// <param name="splitter">Text inserted between values when both keys exist.</param>
        /// <param name="reverseOrder">When <c>true</c>, returns key2+splitter+key1.</param>
        /// <returns>Resolved value, concatenated value, or empty string when keys are missing.</returns>
        public string GetValueFromDataDictionary(KnownEventField key1, KnownEventField? key2 = null, string splitter = "\\", bool reverseOrder = false) {
            return GetValueFromDataDictionary(
                key1.ToEventFieldKey(),
                key2?.ToEventFieldKey(),
                splitter,
                reverseOrder);
        }

        /// <summary>
        /// Gets one or two data values by mixed known/string keys (case-insensitive) and optionally concatenates them.
        /// </summary>
        /// <param name="key1">Primary known field.</param>
        /// <param name="key2">Secondary string key.</param>
        /// <param name="splitter">Text inserted between values when both keys exist.</param>
        /// <param name="reverseOrder">When <c>true</c>, returns key2+splitter+key1.</param>
        /// <returns>Resolved value, concatenated value, or empty string when keys are missing.</returns>
        public string GetValueFromDataDictionary(KnownEventField key1, string? key2 = null, string splitter = "\\", bool reverseOrder = false) {
            return GetValueFromDataDictionary(key1.ToEventFieldKey(), key2, splitter, reverseOrder);
        }

        /// <summary>
        /// Gets one or two data values by mixed string/known keys (case-insensitive) and optionally concatenates them.
        /// </summary>
        /// <param name="key1">Primary string key.</param>
        /// <param name="key2">Secondary known field.</param>
        /// <param name="splitter">Text inserted between values when both keys exist.</param>
        /// <param name="reverseOrder">When <c>true</c>, returns key2+splitter+key1.</param>
        /// <returns>Resolved value, concatenated value, or empty string when keys are missing.</returns>
        public string GetValueFromDataDictionary(string key1, KnownEventField key2, string splitter = "\\", bool reverseOrder = false) {
            return GetValueFromDataDictionary(key1, key2.ToEventFieldKey(), splitter, reverseOrder);
        }

        /// <summary>
        /// Gets a subject account in <c>DOMAIN\user</c> format when available.
        /// </summary>
        /// <param name="splitter">Separator used between domain and user name.</param>
        /// <returns>Subject account, or empty string when both subject fields are missing.</returns>
        public string GetSubjectAccountOrEmpty(string splitter = "\\") {
            return GetValueFromDataDictionary(
                KnownEventField.SubjectUserName,
                KnownEventField.SubjectDomainName,
                splitter,
                reverseOrder: true);
        }

        /// <summary>
        /// Gets a target account in <c>DOMAIN\user</c> format when available.
        /// </summary>
        /// <param name="splitter">Separator used between domain and user name.</param>
        /// <returns>Target account, or empty string when both target fields are missing.</returns>
        public string GetTargetAccountOrEmpty(string splitter = "\\") {
            return GetValueFromDataDictionary(
                KnownEventField.TargetUserName,
                KnownEventField.TargetDomainName,
                splitter,
                reverseOrder: true);
        }

        internal bool ValueMatches(string key, string expectedValue) {
            return TryGetDataValue(key, out string currentValue) &&
                   currentValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetValueFromDictionary(
            IReadOnlyDictionary<string, string>? dictionary,
            string key,
            out string value,
            bool trim = true) {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(key) || dictionary == null || dictionary.Count == 0) {
                return false;
            }

            string? found;
            if (!dictionary.TryGetValue(key, out found)) {
                found = null;
                foreach (KeyValuePair<string, string> kv in dictionary) {
                    if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) {
                        found = kv.Value;
                        break;
                    }
                }
            }

            if (found == null) {
                return false;
            }

            value = trim ? found.Trim() : found;
            return true;
        }

        private static bool TryParseEnumValue<TEnum>(
            string rawValue,
            out TEnum value,
            EventFieldNumericBase numericBase,
            string trimPrefix) where TEnum : struct, Enum {
            value = default;
            if (string.IsNullOrWhiteSpace(rawValue)) {
                return false;
            }

            string normalizedValue = rawValue.Trim();
            if (!string.IsNullOrEmpty(trimPrefix) && normalizedValue.StartsWith(trimPrefix, StringComparison.OrdinalIgnoreCase)) {
                normalizedValue = normalizedValue.Substring(trimPrefix.Length).Trim();
            }

            if (string.IsNullOrWhiteSpace(normalizedValue)) {
                return false;
            }

            if (Enum.TryParse(normalizedValue, true, out value)) {
                return true;
            }

            if (!TryParseUnsignedIntegerValue(normalizedValue, numericBase, out ulong numericValue)) {
                return false;
            }

            return TryConvertToEnumValue(numericValue, out value);
        }

        private static bool TryParseUnsignedIntegerValue(string value, EventFieldNumericBase numericBase, out ulong parsedValue) {
            parsedValue = 0;
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            string normalized = value.Trim();
            bool hasHexPrefix = normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (hasHexPrefix) {
                normalized = normalized.Substring(2);
            }

            if (string.IsNullOrWhiteSpace(normalized)) {
                return false;
            }

            if (numericBase == EventFieldNumericBase.Decimal) {
                return ulong.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue);
            }

            if (numericBase == EventFieldNumericBase.Hexadecimal) {
                return ulong.TryParse(normalized, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out parsedValue);
            }

            if (hasHexPrefix) {
                return ulong.TryParse(normalized, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out parsedValue);
            }

            if (ulong.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue)) {
                return true;
            }

            return ulong.TryParse(normalized, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out parsedValue);
        }

        private static bool TryConvertToEnumValue<TEnum>(ulong rawValue, out TEnum value) where TEnum : struct, Enum {
            value = default;

            Type underlying = Enum.GetUnderlyingType(typeof(TEnum));
            object? typedValue = null;
            switch (Type.GetTypeCode(underlying)) {
                case TypeCode.Byte:
                    if (rawValue > byte.MaxValue) return false;
                    typedValue = (byte)rawValue;
                    break;
                case TypeCode.SByte:
                    if (rawValue > (ulong)sbyte.MaxValue) return false;
                    typedValue = (sbyte)rawValue;
                    break;
                case TypeCode.Int16:
                    if (rawValue > (ulong)short.MaxValue) return false;
                    typedValue = (short)rawValue;
                    break;
                case TypeCode.UInt16:
                    if (rawValue > ushort.MaxValue) return false;
                    typedValue = (ushort)rawValue;
                    break;
                case TypeCode.Int32:
                    if (rawValue > int.MaxValue) return false;
                    typedValue = (int)rawValue;
                    break;
                case TypeCode.UInt32:
                    if (rawValue > uint.MaxValue) return false;
                    typedValue = (uint)rawValue;
                    break;
                case TypeCode.Int64:
                    if (rawValue > long.MaxValue) return false;
                    typedValue = (long)rawValue;
                    break;
                case TypeCode.UInt64:
                    typedValue = rawValue;
                    break;
            }

            if (typedValue == null) {
                return false;
            }

            value = (TEnum)Enum.ToObject(typeof(TEnum), typedValue);
            return true;
        }
    }
}

