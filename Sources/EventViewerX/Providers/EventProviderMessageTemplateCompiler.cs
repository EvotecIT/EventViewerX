using System.Globalization;

namespace EventViewerX.Providers;

/// <summary>
/// Converts friendly named placeholders into Windows event-message insertion
/// strings.
/// </summary>
public static class EventProviderMessageTemplateCompiler {
    /// <summary>
    /// Compiles placeholders such as <c>{ComputerName}</c> to <c>%1</c> using
    /// the canonical payload-field order.
    /// </summary>
    public static string Compile(
        string message,
        IReadOnlyList<EventProviderFieldDefinition> fields) {

        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        if (fields == null) {
            throw new ArgumentNullException(nameof(fields));
        }

        var indexes = fields
            .Select((field, index) => new {
                field.Name,
                Index = index + 1
            })
            .ToDictionary(
                static item => item.Name,
                static item => item.Index,
                StringComparer.OrdinalIgnoreCase);
        var output = new StringBuilder(message.Length + 16);
        for (int index = 0; index < message.Length; index++) {
            char character = message[index];
            if (character == '{') {
                if (index + 1 < message.Length &&
                    message[index + 1] == '{') {
                    output.Append('{');
                    index++;
                    continue;
                }
                int end = message.IndexOf('}', index + 1);
                if (end < 0) {
                    throw new FormatException(
                        "Event message contains an unmatched '{'. Use '{{' for a literal brace.");
                }
                string name = message
                    .Substring(index + 1, end - index - 1)
                    .Trim();
                if (!indexes.TryGetValue(name, out int fieldIndex)) {
                    throw new FormatException(
                        $"Event message references unknown payload field '{name}'.");
                }
                if (fieldIndex > 100) {
                    throw new FormatException(
                        $"Event message references field '{name}' at position {fieldIndex}; Windows event messages support at most 100 insertion strings.");
                }
                output.Append('%');
                output.Append(
                    fieldIndex.ToString(CultureInfo.InvariantCulture));
                index = end;
                continue;
            }
            if (character == '}') {
                if (index + 1 < message.Length &&
                    message[index + 1] == '}') {
                    output.Append('}');
                    index++;
                    continue;
                }
                throw new FormatException(
                    "Event message contains an unmatched '}'. Use '}}' for a literal brace.");
            }
            if (character == '%') {
                throw new FormatException(
                    "Event messages cannot contain a literal '%'. Windows interprets percent sequences while rendering message resources; place percent-bearing text in a named string payload field instead.");
            }
            output.Append(character);
        }
        return output.ToString();
    }
}
