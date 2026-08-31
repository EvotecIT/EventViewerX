using YamlDotNet.RepresentationModel;
using YamlDotNet.Core;

namespace EventViewerX.Sigma;

internal static class SigmaSelectionCompiler {
    private static readonly IReadOnlyDictionary<string, string> CanonicalFields =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["EventID"] = "EventId",
            ["EventId"] = "EventId",
            ["ComputerName"] = "SourceComputer",
            ["Computer"] = "SourceComputer",
            ["Channel"] = "SourceLog",
            ["Provider_Name"] = "ProviderName",
            ["ProviderName"] = "ProviderName",
            ["User"] = "Who",
            ["TargetUserName"] = "TargetUserName",
            ["IpAddress"] = "IpAddress",
            ["SourceIp"] = "IpAddress"
        };

    internal static EventPredicate Compile(YamlNode selection) {
        if (selection is YamlMappingNode mapping) {
            EventPredicate[] fields = mapping.Children.Select(CompileField).ToArray();
            return Combine(fields, requireAll: true);
        }
        if (selection is YamlSequenceNode sequence) {
            EventPredicate[] values = sequence.Children.Select(Compile).ToArray();
            return Combine(values, requireAll: false);
        }
        if (selection is YamlScalarNode scalar) {
            return EventPredicate.Compare(
                "Message",
                EventPredicateOperator.Contains,
                scalar.Value ?? string.Empty);
        }
        throw new SigmaConditionException("EVXSIGMA120", "Unsupported Sigma selection node.");
    }

    private static EventPredicate CompileField(KeyValuePair<YamlNode, YamlNode> item) {
        string expression = Scalar(item.Key, "Sigma selection field");
        string[] parts = expression.Split('|');
        string field = MapField(parts[0]);
        string[] modifiers = parts.Skip(1)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToLowerInvariant())
            .ToArray();
        bool requireAll = modifiers.Contains("all", StringComparer.Ordinal);
        bool ignoreCase = !modifiers.Contains("cased", StringComparer.Ordinal);
        string[] behavior = modifiers.Where(static value => value is not "all" and not "cased").ToArray();
        if (behavior.Length > 1) {
            throw new SigmaConditionException(
                "EVXSIGMA121",
                $"Sigma field '{expression}' combines unsupported modifiers. Only 'all' or 'cased' may accompany one value modifier.");
        }
        string? modifier = behavior.SingleOrDefault();
        if (modifier is not null and not "contains" and not "startswith" and not "endswith" and
            not "re" and not "cidr" and not "exists") {
            throw new SigmaConditionException(
                "EVXSIGMA122",
                $"Sigma modifier '{modifier}' is not supported and was not weakened silently.");
        }
        if (modifier == "exists") {
            string exists = Scalar(item.Value, $"Sigma exists value for '{field}'");
            if (!bool.TryParse(exists, out bool required)) {
                throw new SigmaConditionException("EVXSIGMA123", "Sigma exists modifier requires true or false.");
            }
            return EventPredicate.Compare(
                field,
                required ? EventPredicateOperator.IsNotNull : EventPredicateOperator.IsNull);
        }
        YamlNode[] nodes = item.Value is YamlSequenceNode values
            ? values.Children.ToArray()
            : new[] { item.Value };
        EventPredicate[] comparisons = nodes.Select(value =>
            CreateComparison(field, modifier, ScalarOrNull(value), ignoreCase)).ToArray();
        return Combine(comparisons, requireAll);
    }

    private static EventPredicate CreateComparison(
        string field,
        string? modifier,
        string? value,
        bool ignoreCase) {

        if (value == null) {
            if (modifier != null) {
                throw new SigmaConditionException(
                    "EVXSIGMA128",
                    $"Sigma null selector for '{field}' cannot use the '{modifier}' modifier.");
            }
            return EventPredicate.Compare(field, EventPredicateOperator.IsNull);
        }

        EventPredicateOperator comparison = modifier switch {
            "contains" => EventPredicateOperator.Contains,
            "startswith" => EventPredicateOperator.StartsWith,
            "endswith" => EventPredicateOperator.EndsWith,
            "re" => EventPredicateOperator.MatchesRegex,
            "cidr" => EventPredicateOperator.InSubnet,
            _ when value != null && ContainsWildcard(value) => EventPredicateOperator.MatchesWildcard,
            _ => EventPredicateOperator.Equal
        };
        string? normalizedValue = comparison == EventPredicateOperator.MatchesWildcard
            ? ConvertSigmaWildcard(value ?? string.Empty)
            : value;
        EventPredicate predicate = EventPredicate.Compare(field, comparison, normalizedValue);
        predicate.IgnoreCase = ignoreCase;
        predicate.Validate();
        return predicate;
    }

    internal static int[] GetGuaranteedEventIds(EventPredicate predicate) =>
        TryGetGuaranteedEventIds(predicate, out HashSet<int> eventIds)
            ? eventIds.OrderBy(static eventId => eventId).ToArray()
            : Array.Empty<int>();

    private static bool TryGetGuaranteedEventIds(
        EventPredicate predicate,
        out HashSet<int> eventIds) {

        eventIds = new HashSet<int>();
        if (predicate.Kind == EventPredicateKind.Comparison) {
            if (!string.Equals(predicate.Field, "EventId", StringComparison.OrdinalIgnoreCase) ||
                predicate.Operator is not EventPredicateOperator.Equal and not EventPredicateOperator.In) {
                return false;
            }
            foreach (string? value in predicate.Values) {
                if (!int.TryParse(value, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int eventId) || eventId <= 0) {
                    return false;
                }
                eventIds.Add(eventId);
            }
            return eventIds.Count > 0;
        }
        if (predicate.Kind == EventPredicateKind.Not) {
            return false;
        }
        if (predicate.Kind == EventPredicateKind.All) {
            foreach (EventPredicate child in predicate.Children) {
                if (!TryGetGuaranteedEventIds(child, out HashSet<int> childIds)) {
                    continue;
                }
                if (eventIds.Count == 0) {
                    eventIds.UnionWith(childIds);
                } else {
                    eventIds.IntersectWith(childIds);
                }
            }
            return eventIds.Count > 0;
        }
        foreach (EventPredicate child in predicate.Children) {
            if (!TryGetGuaranteedEventIds(child, out HashSet<int> childIds)) {
                eventIds.Clear();
                return false;
            }
            eventIds.UnionWith(childIds);
        }
        return eventIds.Count > 0;
    }

    private static string MapField(string value) {
        string field = value.Trim();
        if (field.Length == 0) {
            throw new SigmaConditionException("EVXSIGMA124", "Sigma selection field cannot be empty.");
        }
        return CanonicalFields.TryGetValue(field, out string? canonical) ? canonical : field;
    }

    private static bool ContainsWildcard(string value) {
        bool escaped = false;
        foreach (char character in value) {
            if (character == '\\') {
                escaped = !escaped;
                continue;
            }
            if (!escaped && character is '*' or '?') {
                return true;
            }
            escaped = false;
        }
        return false;
    }

    private static string ConvertSigmaWildcard(string value) {
        var result = new System.Text.StringBuilder();
        for (int index = 0; index < value.Length; index++) {
            if (value[index] == '\\' && index + 1 < value.Length && value[index + 1] is '*' or '?' or '\\') {
                char escaped = value[++index];
                if (escaped is '*' or '?') {
                    result.Append('`');
                }
                result.Append(escaped);
                continue;
            }
            result.Append(value[index]);
        }
        return result.ToString();
    }

    private static string Scalar(YamlNode node, string description) {
        if (node is not YamlScalarNode scalar || scalar.Value == null) {
            throw new SigmaConditionException("EVXSIGMA125", description + " must be a scalar value.");
        }
        return scalar.Value;
    }

    private static string? ScalarOrNull(YamlNode node) {
        if (node is not YamlScalarNode scalar) {
            throw new SigmaConditionException("EVXSIGMA126", "Sigma field values must be scalars or scalar lists.");
        }
        if (scalar.Style == ScalarStyle.Plain &&
            (string.IsNullOrEmpty(scalar.Value) ||
             string.Equals(scalar.Value, "~", StringComparison.Ordinal) ||
             string.Equals(scalar.Value, "null", StringComparison.OrdinalIgnoreCase))) {
            return null;
        }
        return scalar.Value;
    }

    private static EventPredicate Combine(IReadOnlyList<EventPredicate> predicates, bool requireAll) {
        if (predicates.Count == 0) {
            throw new SigmaConditionException("EVXSIGMA127", "Sigma selection cannot be empty.");
        }
        if (predicates.Count == 1) {
            return predicates[0];
        }
        return requireAll
            ? EventPredicate.AllOf(predicates.ToArray())
            : EventPredicate.AnyOf(predicates.ToArray());
    }
}
