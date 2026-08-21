using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventViewerX;

/// <summary>
/// Serializable filter tree shared by typed C#, PowerShell, CLI, JSON, reporting, and storage queries.
/// </summary>
public sealed partial class EventPredicate {
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>Node kind.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventPredicateKind Kind { get; set; }

    /// <summary>Field name for a comparison node.</summary>
    public string? Field { get; set; }

    /// <summary>Comparison operation.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventPredicateOperator Operator { get; set; }

    /// <summary>Whether text, wildcard, regex, and enum comparisons ignore case.</summary>
    public bool IgnoreCase { get; set; } = true;

    /// <summary>Invariant comparison values. Null is retained as a real null value.</summary>
    public IReadOnlyList<string?> Values { get; set; } = Array.Empty<string?>();

    /// <summary>Child nodes for Boolean groups.</summary>
    public IReadOnlyList<EventPredicate> Children { get; set; } = Array.Empty<EventPredicate>();

    /// <summary>Creates one field comparison.</summary>
    public static EventPredicate Compare(
        string field,
        EventPredicateOperator comparison,
        params object?[] values) {

        var predicate = new EventPredicate {
            Kind = EventPredicateKind.Comparison,
            Field = NormalizeField(field),
            Operator = comparison,
            Values = (values ?? Array.Empty<object?>())
                .Select(ToInvariantString)
                .ToArray()
        };
        predicate.Validate();
        return predicate;
    }

    /// <summary>Creates a group that requires every child to match.</summary>
    public static EventPredicate AllOf(params EventPredicate[] predicates) =>
        CreateGroup(EventPredicateKind.All, predicates);

    /// <summary>Creates a group that requires at least one child to match.</summary>
    public static EventPredicate AnyOf(params EventPredicate[] predicates) =>
        CreateGroup(EventPredicateKind.Any, predicates);

    /// <summary>Negates one predicate.</summary>
    public static EventPredicate Not(EventPredicate predicate) =>
        CreateGroup(EventPredicateKind.Not, predicate);

    /// <summary>Loads and validates a predicate from JSON text.</summary>
    public static EventPredicate ParseJson(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            throw new ArgumentException("Predicate JSON cannot be empty.", nameof(json));
        }
        EventPredicate? predicate = JsonSerializer.Deserialize<EventPredicate>(json, JsonOptions);
        if (predicate == null) {
            throw new InvalidDataException("Predicate JSON did not contain an object.");
        }
        predicate.Validate();
        return predicate;
    }

    /// <summary>Loads and validates a predicate from a JSON file.</summary>
    public static EventPredicate Load(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Predicate path cannot be empty.", nameof(path));
        }
        return ParseJson(File.ReadAllText(Path.GetFullPath(path)));
    }

    /// <summary>Serializes this predicate as JSON.</summary>
    public string ToJson(bool indented = true) {
        Validate();
        JsonSerializerOptions options = CreateJsonOptions();
        options.WriteIndented = indented;
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>Saves this predicate to a JSON file.</summary>
    public void Save(string path, bool indented = true) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Predicate path cannot be empty.", nameof(path));
        }
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory!);
        }
        File.WriteAllText(fullPath, ToJson(indented));
    }

    /// <summary>Creates a detached validated copy.</summary>
    public EventPredicate Clone() => ParseJson(ToJson(indented: false));

    /// <summary>Validates the complete predicate tree.</summary>
    public void Validate() {
        ValidateNode(this, "Predicate", depth: 0, refCount: new NodeCounter());
    }

    private static EventPredicate CreateGroup(
        EventPredicateKind kind,
        params EventPredicate[] predicates) {

        var group = new EventPredicate {
            Kind = kind,
            Children = predicates?.ToArray() ?? Array.Empty<EventPredicate>()
        };
        group.Validate();
        return group;
    }

    private static void ValidateNode(
        EventPredicate predicate,
        string path,
        int depth,
        NodeCounter refCount) {

        if (depth > 32) {
            throw new InvalidDataException("Predicate nesting cannot exceed 32 levels.");
        }
        refCount.Value++;
        if (refCount.Value > 512) {
            throw new InvalidDataException("A predicate cannot contain more than 512 nodes.");
        }
        if (!Enum.IsDefined(typeof(EventPredicateKind), predicate.Kind)) {
            throw new InvalidDataException($"{path}.Kind is not supported.");
        }
        predicate.Values ??= Array.Empty<string?>();
        predicate.Children ??= Array.Empty<EventPredicate>();
        if (predicate.Kind == EventPredicateKind.Comparison) {
            predicate.Field = NormalizeField(predicate.Field);
            if (!Enum.IsDefined(typeof(EventPredicateOperator), predicate.Operator)) {
                throw new InvalidDataException($"{path}.Operator is not supported.");
            }
            if (predicate.Children.Count != 0) {
                throw new InvalidDataException($"{path}.Children must be empty for a comparison.");
            }
            int requiredValues = predicate.Operator is EventPredicateOperator.IsNull or EventPredicateOperator.IsNotNull
                ? 0
                : 1;
            if (predicate.Values.Count < requiredValues) {
                throw new InvalidDataException($"{path}.Values requires at least one value for {predicate.Operator}.");
            }
            if (requiredValues == 0 && predicate.Values.Count != 0) {
                throw new InvalidDataException($"{path}.Values must be empty for {predicate.Operator}.");
            }
            if (predicate.Operator is not EventPredicateOperator.In and not EventPredicateOperator.NotIn &&
                requiredValues == 1 && predicate.Values.Count != 1) {
                throw new InvalidDataException($"{path}.Values requires exactly one value for {predicate.Operator}.");
            }
            if (predicate.Values.Count == 1 && predicate.Values[0] == null &&
                predicate.Operator is not EventPredicateOperator.Equal and
                not EventPredicateOperator.NotEqual and
                not EventPredicateOperator.In and
                not EventPredicateOperator.NotIn) {
                throw new InvalidDataException(
                    $"{path}.Values[0] cannot be null for {predicate.Operator}. " +
                    "Use IsNull or IsNotNull for null selection.");
            }
            if (predicate.Values.Count > 1024) {
                throw new InvalidDataException($"{path}.Values cannot contain more than 1024 values.");
            }
            if (predicate.Values.Any(static value => value != null && value.Length > 4096)) {
                throw new InvalidDataException($"{path}.Values cannot contain text longer than 4096 characters.");
            }
            ValidatePattern(predicate, path);
            return;
        }

        if (!string.IsNullOrWhiteSpace(predicate.Field) || predicate.Values.Count != 0) {
            throw new InvalidDataException($"{path} Boolean groups cannot declare Field or Values.");
        }
        predicate.Children ??= Array.Empty<EventPredicate>();
        int requiredChildren = predicate.Kind == EventPredicateKind.Not ? 1 : 2;
        if (predicate.Children.Count < requiredChildren ||
            predicate.Kind == EventPredicateKind.Not && predicate.Children.Count != 1) {
            throw new InvalidDataException(
                predicate.Kind == EventPredicateKind.Not
                    ? $"{path}.Children requires exactly one child for Not."
                    : $"{path}.Children requires at least two children for {predicate.Kind}.");
        }
        for (int index = 0; index < predicate.Children.Count; index++) {
            EventPredicate child = predicate.Children[index] ??
                throw new InvalidDataException($"{path}.Children[{index}] cannot be null.");
            ValidateNode(child, $"{path}.Children[{index}]", depth + 1, refCount);
        }
    }

    private static string NormalizeField(string? field) {
        string normalized = field?.Trim() ?? string.Empty;
        if (normalized.Length == 0) {
            throw new InvalidDataException("Predicate Field is required.");
        }
        if (normalized.Length > 256) {
            throw new InvalidDataException("Predicate Field cannot exceed 256 characters.");
        }
        return normalized;
    }

    private static string? ToInvariantString(object? value) {
        if (value == null) {
            return null;
        }
        if (value is DateTime dateTime) {
            return dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
        if (value is DateTimeOffset dateTimeOffset) {
            return dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
    }

    private static JsonSerializerOptions CreateJsonOptions() => new() {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static void ValidatePattern(EventPredicate predicate, string path) {
        string? value = predicate.Values.Count == 0 ? null : predicate.Values[0];
        if (predicate.Operator is EventPredicateOperator.MatchesRegex or EventPredicateOperator.MatchesWildcard &&
            value != null && value.Length > 2048) {
            throw new InvalidDataException($"{path}.Values[0] cannot exceed 2048 characters for pattern matching.");
        }
        if (predicate.Operator == EventPredicateOperator.MatchesRegex) {
            try {
                _ = new Regex(
                    value ?? string.Empty,
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException exception) {
                throw new InvalidDataException($"{path}.Values[0] is not a valid regular expression.", exception);
            }
        }
        if (predicate.Operator == EventPredicateOperator.InSubnet && !IsValidSubnet(value)) {
            throw new InvalidDataException($"{path}.Values[0] must be a valid IPv4 or IPv6 CIDR subnet.");
        }
    }

    private static bool IsValidSubnet(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }
        string[] parts = value!.Split('/');
        return parts.Length == 2 &&
               IPAddress.TryParse(parts[0], out IPAddress? address) &&
               int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prefix) &&
               prefix >= 0 && prefix <= address.GetAddressBytes().Length * 8;
    }

    private sealed class NodeCounter {
        internal int Value;
    }
}
