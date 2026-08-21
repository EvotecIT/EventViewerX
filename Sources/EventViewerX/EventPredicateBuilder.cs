namespace EventViewerX;

/// <summary>
/// Type-aware builder that exposes the fields available to one built-in or custom event definition.
/// </summary>
public sealed class EventPredicateBuilder {
    private readonly IReadOnlyDictionary<string, EventPredicateField> _fields;

    private EventPredicateBuilder(
        string definitionName,
        string displayName,
        IEnumerable<EventFieldDefinition> fields) {

        DefinitionName = definitionName;
        DisplayName = displayName;
        EventPredicateField[] values = fields
            .Where(static field => field.IsFilterable)
            .Select(static field => new EventPredicateField(field))
            .ToArray();
        Fields = values;
        var fieldLookup = new Dictionary<string, EventPredicateField>(StringComparer.OrdinalIgnoreCase);
        foreach (EventPredicateField field in values) {
            fieldLookup[field.Name] = field;
            foreach (string alias in field.Definition.Aliases) {
                if (!string.IsNullOrWhiteSpace(alias)) {
                    string normalizedAlias = alias.Trim();
                    if (!fieldLookup.ContainsKey(normalizedAlias)) {
                        fieldLookup[normalizedAlias] = field;
                    }
                }
            }
        }
        _fields = fieldLookup;
    }

    /// <summary>Stable definition name.</summary>
    public string DefinitionName { get; }

    /// <summary>Human-friendly definition label.</summary>
    public string DisplayName { get; }

    /// <summary>Filterable fields in stable projection order.</summary>
    public IReadOnlyList<EventPredicateField> Fields { get; }

    /// <summary>Creates a builder for one built-in event type.</summary>
    public static EventPredicateBuilder ForType(EventType type) => ForTypes(new[] { type });

    /// <summary>Creates a builder exposing the union of fields for one or more built-in event types.</summary>
    public static EventPredicateBuilder ForTypes(IEnumerable<EventType> types) {
        EventType[] requested = types?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(types));
        if (requested.Length == 0) {
            throw new ArgumentException("At least one event type is required.", nameof(types));
        }
        EventTypeDefinition[] definitions = requested.Select(EventTypeCatalog.GetDefinition).ToArray();
        IReadOnlyList<EventFieldDefinition> fields = EventTypeCatalog
            .Expand(requested)
            .Select(EventTypeCatalog.GetDefinition)
            .SelectMany(static item => item.Fields)
            .GroupBy(static field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        string name = definitions.Length == 1
            ? definitions[0].Name
            : string.Join(",", definitions.Select(static definition => definition.Name));
        string displayName = definitions.Length == 1
            ? definitions[0].DisplayName
            : "Multiple event types";
        return new EventPredicateBuilder(name, displayName, fields);
    }

    /// <summary>Creates a builder for one declarative custom definition.</summary>
    public static EventPredicateBuilder ForDefinition(EventDefinition definition) {
        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        definition.Validate();
        EventFieldDefinition[] customFields = definition.Fields.Select(
            static field => new EventFieldDefinition(
                field.Name,
                string.IsNullOrWhiteSpace(field.DisplayName) ? field.Name : field.DisplayName,
                field.ValueType,
                isCommon: false,
                field.Description,
                field.Aliases,
                EventDefinitionEngine.CanUseNativeMetadataField(field)
                    ? EventFieldFilterStage.Native
                    : EventFieldFilterStage.Managed)).ToArray();
        var claimedCommonNames = new HashSet<string>(
            customFields.SelectMany(static field => field.Aliases.Prepend(field.Name)),
            StringComparer.OrdinalIgnoreCase);
        IEnumerable<EventFieldDefinition> fields = customFields
            .Concat(EventTypeCatalog.GetCommonFields().Where(field => !claimedCommonNames.Contains(field.Name)))
            .GroupBy(static field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First());
        return new EventPredicateBuilder(definition.Name, definition.DisplayName, fields);
    }

    /// <summary>
    /// Creates a builder from a discovered projection schema, including the common EventViewerX fields.
    /// This is intended for durable or remote schemas whose original definition object is unavailable.
    /// </summary>
    public static EventPredicateBuilder ForFields(
        string definitionName,
        IEnumerable<KeyValuePair<string, Type>> fields,
        string? displayName = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? aliases = null) {

        if (string.IsNullOrWhiteSpace(definitionName)) {
            throw new ArgumentException("Definition name cannot be empty.", nameof(definitionName));
        }
        if (fields == null) {
            throw new ArgumentNullException(nameof(fields));
        }
        KeyValuePair<string, Type>[] snapshot = fields.ToArray();
        if (snapshot.Any(static field => string.IsNullOrWhiteSpace(field.Key) || field.Value == null)) {
            throw new ArgumentException("Projection fields must have non-empty names and value types.", nameof(fields));
        }
        string[] duplicates = snapshot
            .GroupBy(static field => field.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicates.Length > 0) {
            throw new ArgumentException(
                "Projection fields contain duplicate case-insensitive names: " +
                string.Join(", ", duplicates) + ".",
                nameof(fields));
        }
        var claimedIdentities = new HashSet<string>(
            snapshot.Select(static field => field.Key.Trim()),
            StringComparer.OrdinalIgnoreCase);
        EventFieldDefinition[] projected = snapshot.Select(field => {
            IReadOnlyList<string> fieldAliases = aliases != null &&
                aliases.TryGetValue(field.Key, out IReadOnlyList<string>? configured)
                ? configured
                    .Where(static alias => !string.IsNullOrWhiteSpace(alias))
                    .Select(static alias => alias.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : Array.Empty<string>();
            foreach (string alias in fieldAliases) {
                if (!claimedIdentities.Add(alias)) {
                    throw new ArgumentException(
                        $"Projection fields contain duplicate field or alias '{alias}'.",
                        nameof(aliases));
                }
            }
            return new EventFieldDefinition(
                field.Key.Trim(),
                field.Key.Trim(),
                field.Value,
                isCommon: false,
                aliases: fieldAliases);
        }).ToArray();
        var claimedCommonNames = new HashSet<string>(
            projected.SelectMany(static field => field.Aliases.Prepend(field.Name)),
            StringComparer.OrdinalIgnoreCase);
        IEnumerable<EventFieldDefinition> allFields = projected
            .Concat(EventTypeCatalog.GetCommonFields().Where(field => !claimedCommonNames.Contains(field.Name)))
            .GroupBy(static field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First());
        string normalizedName = definitionName.Trim();
        return new EventPredicateBuilder(
            normalizedName,
            string.IsNullOrWhiteSpace(displayName) ? normalizedName : displayName!.Trim(),
            allFields);
    }

    /// <summary>Gets one field by stable name, ignoring case.</summary>
    public EventPredicateField Field(string name) {
        if (string.IsNullOrWhiteSpace(name) || !_fields.TryGetValue(name.Trim(), out EventPredicateField? field)) {
            throw new ArgumentException(
                $"Field '{name}' is not available for definition '{DefinitionName}'. " +
                $"Available fields: {string.Join(", ", Fields.Select(static item => item.Name))}.",
                nameof(name));
        }
        return field;
    }

    /// <summary>Validates field names and operators and returns a canonical independent predicate.</summary>
    public EventPredicate Normalize(EventPredicate predicate) {
        if (predicate == null) {
            throw new ArgumentNullException(nameof(predicate));
        }
        EventPredicate normalized = predicate.Clone();
        normalized.Validate();
        NormalizeNode(normalized);
        return normalized;
    }

    private void NormalizeNode(EventPredicate predicate) {
        if (predicate.Kind == EventPredicateKind.Comparison) {
            EventPredicateField field = Field(predicate.Field!);
            if (!field.SupportedOperators.Contains(predicate.Operator)) {
                throw new ArgumentException(
                    $"Operator '{predicate.Operator}' is not supported by field '{field.Name}'. " +
                    $"Supported operators: {string.Join(", ", field.SupportedOperators)}.",
                    nameof(predicate));
            }
            ValidateLiterals(field, predicate);
            predicate.Field = field.Name;
        }
        foreach (EventPredicate child in predicate.Children) {
            NormalizeNode(child);
        }
    }

    private static void ValidateLiterals(
        EventPredicateField field,
        EventPredicate predicate) {

        if (predicate.Operator is EventPredicateOperator.IsNull or EventPredicateOperator.IsNotNull ||
            predicate.Operator is EventPredicateOperator.StartsWith or EventPredicateOperator.EndsWith or
            EventPredicateOperator.MatchesWildcard or EventPredicateOperator.MatchesRegex or
            EventPredicateOperator.InSubnet ||
            field.ValueType == typeof(string)) {
            return;
        }
        Type valueType = field.ValueType;
        if (EventFieldDefinition.TryGetEnumerableElementType(valueType, out Type? elementType) &&
            elementType != null) {
            valueType = elementType;
        }
        for (int index = 0; index < predicate.Values.Count; index++) {
            if (!EventPredicateEvaluator.TryConvertExpected(
                    predicate.Values[index],
                    valueType,
                    predicate.IgnoreCase,
                    out _)) {
                throw new ArgumentException(
                    $"Value '{predicate.Values[index]}' is not valid for field '{field.Name}' " +
                    $"of type '{valueType.Name}'.",
                    nameof(predicate));
            }
        }
    }

    /// <summary>Requires every predicate to match.</summary>
    public EventPredicate AllOf(params EventPredicate[] predicates) =>
        EventPredicate.AllOf(predicates);

    /// <summary>Requires at least one predicate to match.</summary>
    public EventPredicate AnyOf(params EventPredicate[] predicates) =>
        EventPredicate.AnyOf(predicates);

    /// <summary>Negates one predicate.</summary>
    public EventPredicate Not(EventPredicate predicate) => EventPredicate.Not(predicate);

}
