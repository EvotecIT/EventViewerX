namespace EventViewerX.Sigma;

internal static class SigmaConditionCompiler {
    internal static EventPredicate Compile(
        string condition,
        IReadOnlyDictionary<string, EventPredicate> selections) {

        if (string.IsNullOrWhiteSpace(condition)) {
            throw new SigmaConditionException("EVXSIGMA100", "Sigma detection.condition is required.");
        }
        var parser = new Parser(Tokenize(condition), selections);
        EventPredicate predicate = parser.ParseExpression();
        parser.RequireEnd();
        return predicate;
    }

    private static string[] Tokenize(string condition) {
        var tokens = new List<string>();
        int index = 0;
        while (index < condition.Length) {
            char value = condition[index];
            if (char.IsWhiteSpace(value)) {
                index++;
                continue;
            }
            if (value is '(' or ')') {
                tokens.Add(value.ToString());
                index++;
                continue;
            }
            int start = index;
            while (index < condition.Length &&
                   !char.IsWhiteSpace(condition[index]) &&
                   condition[index] is not '(' and not ')') {
                index++;
            }
            tokens.Add(condition.Substring(start, index - start));
        }
        return tokens.ToArray();
    }

    private sealed class Parser {
        private readonly string[] _tokens;
        private readonly IReadOnlyDictionary<string, EventPredicate> _selections;
        private int _position;

        internal Parser(string[] tokens, IReadOnlyDictionary<string, EventPredicate> selections) {
            _tokens = tokens;
            _selections = selections;
        }

        internal EventPredicate ParseExpression() => ParseOr();

        internal void RequireEnd() {
            if (_position != _tokens.Length) {
                throw new SigmaConditionException(
                    "EVXSIGMA101",
                    $"Unsupported Sigma condition token '{_tokens[_position]}'.");
            }
        }

        private EventPredicate ParseOr() {
            var values = new List<EventPredicate> { ParseAnd() };
            while (Take("or")) {
                values.Add(ParseAnd());
            }
            return Combine(values, requireAll: false);
        }

        private EventPredicate ParseAnd() {
            var values = new List<EventPredicate> { ParseUnary() };
            while (Take("and")) {
                values.Add(ParseUnary());
            }
            return Combine(values, requireAll: true);
        }

        private EventPredicate ParseUnary() {
            if (Take("not")) {
                return EventPredicate.Not(ParseUnary());
            }
            if (Take("(")) {
                EventPredicate nested = ParseExpression();
                Require(")");
                return nested;
            }
            string token = Next();
            if ((string.Equals(token, "all", StringComparison.OrdinalIgnoreCase) ||
                 int.TryParse(token, out _)) &&
                Take("of")) {
                return ParseQuantifier(token, Next());
            }
            if (!_selections.TryGetValue(token, out EventPredicate? predicate)) {
                throw new SigmaConditionException(
                    "EVXSIGMA102",
                    $"Sigma condition references unknown selection '{token}'.");
            }
            return predicate.Clone();
        }

        private EventPredicate ParseQuantifier(string countToken, string pattern) {
            bool requireAll = string.Equals(countToken, "all", StringComparison.OrdinalIgnoreCase);
            if (!requireAll && !string.Equals(countToken, "1", StringComparison.Ordinal)) {
                throw new SigmaConditionException(
                    "EVXSIGMA103",
                    "EventViewerX supports '1 of' and 'all of' Sigma selection quantifiers; larger numeric quantifiers are rejected.");
            }
            IEnumerable<KeyValuePair<string, EventPredicate>> matches;
            if (string.Equals(pattern, "them", StringComparison.OrdinalIgnoreCase)) {
                matches = _selections;
            } else if (pattern.EndsWith("*", StringComparison.Ordinal)) {
                string prefix = pattern.Substring(0, pattern.Length - 1);
                matches = _selections.Where(item => item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            } else {
                matches = _selections.Where(item => string.Equals(item.Key, pattern, StringComparison.OrdinalIgnoreCase));
            }
            EventPredicate[] predicates = matches.Select(static item => item.Value.Clone()).ToArray();
            if (predicates.Length == 0) {
                throw new SigmaConditionException(
                    "EVXSIGMA104",
                    $"Sigma condition pattern '{pattern}' did not match any selection.");
            }
            return Combine(predicates, requireAll);
        }

        private bool Take(string expected) {
            if (_position >= _tokens.Length ||
                !string.Equals(_tokens[_position], expected, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            _position++;
            return true;
        }

        private string Next() {
            if (_position >= _tokens.Length) {
                throw new SigmaConditionException("EVXSIGMA105", "Sigma condition ended unexpectedly.");
            }
            return _tokens[_position++];
        }

        private void Require(string expected) {
            if (!Take(expected)) {
                throw new SigmaConditionException("EVXSIGMA106", $"Sigma condition requires '{expected}'.");
            }
        }

        private static EventPredicate Combine(IEnumerable<EventPredicate> source, bool requireAll) {
            EventPredicate[] predicates = source.ToArray();
            if (predicates.Length == 1) {
                return predicates[0];
            }
            return requireAll ? EventPredicate.AllOf(predicates) : EventPredicate.AnyOf(predicates);
        }
    }
}

internal sealed class SigmaConditionException : Exception {
    internal SigmaConditionException(string code, string message) : base(message) {
        Code = code;
    }

    internal string Code { get; }
}
