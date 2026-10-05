using System.Text;
using System.Text.RegularExpressions;

namespace EventViewerX.Sigma;

/// <summary>Preserves Sigma escaping independently of PowerShell wildcard syntax.</summary>
internal static class SigmaStringPattern {
    internal static EventPredicate Compile(string field, string value, string? modifier, bool ignoreCase) {
        var literal = new StringBuilder();
        var pattern = new StringBuilder("(?s)");
        if (modifier is null or "startswith") { pattern.Append("\\A"); }
        bool wildcard = false;
        for (int index = 0; index < value.Length; index++) {
            char current = value[index];
            if (current == '\\' && index + 1 < value.Length && value[index + 1] is '\\' or '*' or '?') {
                current = value[++index];
            } else if (current is '*' or '?') {
                wildcard = true;
                pattern.Append(current == '*' ? ".*" : ".");
                continue;
            }
            literal.Append(current);
            pattern.Append(Regex.Escape(current.ToString()));
        }
        if (modifier is null or "endswith") { pattern.Append("\\z"); }
        EventPredicateOperator comparison = wildcard ? EventPredicateOperator.MatchesRegex : modifier switch {
            "contains" => EventPredicateOperator.Contains,
            "startswith" => EventPredicateOperator.StartsWith,
            "endswith" => EventPredicateOperator.EndsWith,
            _ => EventPredicateOperator.Equal
        };
        EventPredicate result = EventPredicate.Compare(field, comparison, wildcard ? pattern.ToString() : literal.ToString());
        result.IgnoreCase = ignoreCase;
        result.Validate();
        return result;
    }
}
