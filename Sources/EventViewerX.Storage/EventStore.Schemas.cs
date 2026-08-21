using System.Security.Cryptography;
using System.Text.Json;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    private static readonly IReadOnlyDictionary<string, EventTypeDefinition> BuiltInDefinitions =
        EventTypeCatalog.GetDefinitions().ToDictionary(
            static definition => definition.Name,
            StringComparer.OrdinalIgnoreCase);

    private static EventReportSectionSchema[] NormalizeIncomingSchemas(
        IReadOnlyList<EventReportSectionSchema> schemas) {

        var normalized = new List<EventReportSectionSchema>(schemas.Count);
        var byName = new Dictionary<string, EventReportSectionSchema>(StringComparer.OrdinalIgnoreCase);
        foreach (EventReportSectionSchema schema in schemas) {
            ValidateSchema(schema);
            if (!byName.TryGetValue(schema.Name, out EventReportSectionSchema? existing)) {
                byName.Add(schema.Name, schema);
                normalized.Add(schema);
                continue;
            }
            if (existing.Kind == EventReportSectionKind.Generic &&
                schema.Kind == EventReportSectionKind.Generic) {
                EventReportSectionSchema merged = MergeGenericSchemas(existing, schema);
                int index = normalized.IndexOf(existing);
                normalized[index] = merged;
                byName[schema.Name] = merged;
                continue;
            }
            if (existing.Kind != schema.Kind ||
                !string.Equals(CreateSchemaHash(existing), CreateSchemaHash(schema), StringComparison.Ordinal)) {
                throw new InvalidDataException(
                    $"Incoming report contains conflicting schemas for definition '{existing.Name}'.");
            }
        }
        return normalized.ToArray();
    }

    private static void ValidateSchema(EventReportSectionSchema schema) {
        if (string.IsNullOrWhiteSpace(schema.Name)) {
            throw new InvalidDataException("Incoming report contains a schema without a name.");
        }
        if (!Enum.IsDefined(typeof(EventReportSectionKind), schema.Kind)) {
            throw new InvalidDataException(
                $"Incoming schema '{schema.Name}' contains an undefined section kind.");
        }
        if (schema.Kind == EventReportSectionKind.Generic &&
            !string.Equals(schema.Name, "Generic", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("The generic stored schema must use the stable name 'Generic'.");
        }
        if (schema.Kind != EventReportSectionKind.Generic &&
            string.Equals(schema.Name, "Generic", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("The stable definition name 'Generic' is reserved for generic event rows.");
        }
        if (schema.Kind == EventReportSectionKind.Custom &&
            (BuiltInDefinitions.ContainsKey(schema.Name) ||
             string.Equals(schema.Name, "EventStoreSummary", StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidDataException(
                $"Custom definition '{schema.Name}' collides with a reserved EventViewerX definition.");
        }
        if (schema.Kind == EventReportSectionKind.Typed) {
            if (!BuiltInDefinitions.TryGetValue(schema.Name, out EventTypeDefinition? definition) ||
                definition.IsComposite) {
                throw new InvalidDataException(
                    $"Typed schema '{schema.Name}' must identify one built-in leaf EventViewerX definition.");
            }
            EventReportSectionSchema expected = EventReportSectionSchema.FromType(definition.Type);
            if (!string.Equals(CreateSchemaHash(schema), CreateSchemaHash(expected), StringComparison.Ordinal)) {
                throw new InvalidDataException(
                    $"Typed schema '{schema.Name}' does not match the built-in EventViewerX report contract.");
            }
        }
        string[] duplicateColumns = schema.Columns
            .GroupBy(static column => column.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateColumns.Length > 0) {
            throw new InvalidDataException(
                $"Incoming schema '{schema.Name}' contains duplicate case-insensitive columns: " +
                string.Join(", ", duplicateColumns) + ".");
        }
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < schema.Columns.Count; index++) {
            EventReportColumnSchema column = schema.Columns[index];
            if (!identities.Add(column.Name)) {
                continue;
            }
            IReadOnlyList<string> aliases = column.Aliases ?? Array.Empty<string>();
            if (aliases.Any(static alias => string.IsNullOrWhiteSpace(alias))) {
                throw new InvalidDataException(
                    $"Incoming schema '{schema.Name}' column '{column.Name}' contains an empty alias.");
            }
            foreach (string alias in aliases) {
                if (!identities.Add(alias.Trim())) {
                    throw new InvalidDataException(
                        $"Incoming schema '{schema.Name}' contains duplicate field or alias '{alias.Trim()}'.");
                }
            }
        }
    }

    private static EventReportSectionSchema MergeGenericSchemas(
        EventReportSectionSchema existing,
        EventReportSectionSchema incoming) {

        var columns = new List<EventReportColumnSchema>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EventReportColumnSchema column in existing.Columns.Concat(incoming.Columns)) {
            if (known.Add(column.Name)) {
                columns.Add(new EventReportColumnSchema {
                    Name = column.Name,
                    DisplayName = column.DisplayName,
                    ValueTypeName = column.ValueTypeName,
                    Aliases = column.Aliases?.ToArray() ?? Array.Empty<string>()
                });
            }
        }
        return new EventReportSectionSchema {
            Name = existing.Name,
            DisplayName = string.IsNullOrWhiteSpace(incoming.DisplayName)
                ? existing.DisplayName
                : incoming.DisplayName,
            Description = string.IsNullOrWhiteSpace(incoming.Description)
                ? existing.Description
                : incoming.Description,
            Kind = EventReportSectionKind.Generic,
            Columns = columns
        };
    }

    private static EventReportSectionSchema DeserializeStoredSchema(StoredDefinitionSchema definition) {
        return DeserializeStoredSchema(definition.Name, definition.Json);
    }

    private static EventReportSectionSchema DeserializeStoredSchema(string definitionName, string json) {
        try {
            EventReportSectionSchema? schema = JsonSerializer.Deserialize<EventReportSectionSchema>(
                json,
                JsonOptions);
            if (schema == null) {
                throw new InvalidDataException($"Stored definition '{definitionName}' has an invalid schema.");
            }
            if (!Enum.IsDefined(typeof(EventReportSectionKind), schema.Kind) ||
                schema.Columns == null ||
                schema.Columns.Any(static column => column == null || string.IsNullOrWhiteSpace(column.Name))) {
                throw new InvalidDataException($"Stored definition '{definitionName}' has an invalid schema.");
            }
            if (schema.Kind != EventReportSectionKind.Generic) {
                string? duplicate = schema.Columns
                    .GroupBy(static column => column.Name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(static group => group.Count() > 1)?.Key;
                if (duplicate != null) {
                    throw new InvalidDataException(
                        $"Stored definition '{definitionName}' contains duplicate case-insensitive column '{duplicate}'.");
                }
            }
            schema.Name = definitionName;
            schema.DisplayName = string.IsNullOrWhiteSpace(schema.DisplayName)
                ? definitionName
                : schema.DisplayName.Trim();
            schema.Description = schema.Description?.Trim() ?? string.Empty;
            foreach (EventReportColumnSchema column in schema.Columns) {
                column.Name = column.Name.Trim();
                column.DisplayName = string.IsNullOrWhiteSpace(column.DisplayName)
                    ? column.Name
                    : column.DisplayName.Trim();
                column.ValueTypeName = EventReportColumnSchema.NormalizeValueTypeName(column.ValueTypeName);
                column.Aliases = (column.Aliases ?? Array.Empty<string>())
                    .Where(static alias => !string.IsNullOrWhiteSpace(alias))
                    .Select(static alias => alias.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            ValidateSchema(schema);
            if (schema.Kind == EventReportSectionKind.Generic) {
                schema = MergeGenericSchemas(schema, schema);
            }
            return schema;
        } catch (JsonException exception) {
            throw new InvalidDataException(
                $"Stored definition '{definitionName}' has an invalid schema.",
                exception);
        }
    }

    private static string CreateSchemaHash(EventReportSectionSchema schema) {
        if (schema.Kind == EventReportSectionKind.Generic) {
            return "generic-dynamic-v1";
        }
        string identity = string.Join("\0", new[] {
            ((int)schema.Kind).ToString(CultureInfo.InvariantCulture)
        }.Concat(schema.Columns.SelectMany(static column => new[] {
            column.Name,
            EventReportColumnSchema.NormalizeValueTypeName(column.ValueTypeName),
            string.Join(",", (column.Aliases ?? Array.Empty<string>())
                .OrderBy(static alias => alias, StringComparer.OrdinalIgnoreCase))
        })));
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
        return string.Concat(hash.Select(static value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static bool HasEquivalentSchema(string json, string expectedHash) {
        try {
            EventReportSectionSchema? schema = JsonSerializer.Deserialize<EventReportSectionSchema>(json, JsonOptions);
            return schema != null &&
                   string.Equals(CreateSchemaHash(schema), expectedHash, StringComparison.Ordinal);
        } catch (JsonException) {
            return false;
        }
    }

    private sealed class StoredDefinitionSchema {
        internal StoredDefinitionSchema(string name, string hash, string json) {
            Name = name;
            Hash = hash;
            Json = json;
        }

        internal string Name { get; }
        internal string Hash { get; }
        internal string Json { get; }
    }
}
