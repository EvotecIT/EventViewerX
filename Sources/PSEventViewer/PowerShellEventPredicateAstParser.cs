using System.Management.Automation.Language;

namespace PSEventViewer;

/// <summary>Translates a restricted, non-executing PowerShell filter AST into EventPredicate.</summary>
internal static class PowerShellEventPredicateAstParser {
    internal static EventPredicate Parse(
        ScriptBlock scriptBlock,
        EventPredicateBuilder? builder = null) {
        if (scriptBlock == null) {
            throw new ArgumentNullException(nameof(scriptBlock));
        }
        if (scriptBlock.Ast is not ScriptBlockAst scriptBlockAst) {
            throw Unsupported("Where did not contain a PowerShell script block AST.");
        }
        NamedBlockAst? body = scriptBlockAst.EndBlock;
        if (body == null || body.Statements.Count != 1 || body.Traps?.Count > 0) {
            throw Unsupported("Where must contain exactly one expression and cannot contain traps.");
        }
        if (body.Statements[0] is not PipelineAst pipeline ||
            pipeline.PipelineElements.Count != 1 ||
            pipeline.PipelineElements[0] is not CommandExpressionAst commandExpression) {
            throw Unsupported("Where must contain one comparison expression, not commands or pipelines.");
        }
        EventPredicate predicate = Translate(commandExpression.Expression, builder);
        predicate.Validate();
        return predicate;
    }

    private static EventPredicate Translate(
        ExpressionAst expression,
        EventPredicateBuilder? builder) {

        expression = Unwrap(expression);
        if (expression is BinaryExpressionAst binary) {
            if (binary.Operator == TokenKind.And) {
                return EventPredicate.AllOf(
                    Translate(binary.Left, builder),
                    Translate(binary.Right, builder));
            }
            if (binary.Operator == TokenKind.Or) {
                return EventPredicate.AnyOf(
                    Translate(binary.Left, builder),
                    Translate(binary.Right, builder));
            }
            return TranslateComparison(binary, builder);
        }
        if (expression is UnaryExpressionAst unary &&
            unary.TokenKind is TokenKind.Not or TokenKind.Exclaim) {
            return EventPredicate.Not(Translate(unary.Child, builder));
        }
        throw Unsupported($"Expression '{expression.Extent.Text}' is not a supported typed filter.");
    }

    private static EventPredicate TranslateComparison(
        BinaryExpressionAst binary,
        EventPredicateBuilder? builder) {

        bool fieldOnLeft = TryReadField(binary.Left, out string? leftField);
        bool fieldOnRight = TryReadField(binary.Right, out string? rightField);
        if (fieldOnLeft == fieldOnRight) {
            throw Unsupported("Each comparison must contain exactly one $_.Field member.");
        }
        if (fieldOnRight && binary.Operator is
                TokenKind.Icontains or TokenKind.Ccontains or
                TokenKind.Inotcontains or TokenKind.Cnotcontains) {
            throw Unsupported(
                "Reversed -contains/-notcontains changes meaning for collection-valued fields. " +
                "Use 'value' -in $_.Field for collection membership, or $_.Field -in @('value1', 'value2') for scalar selection.");
        }
        if (fieldOnRight && builder != null && binary.Operator is
                TokenKind.Ieq or TokenKind.Ceq or TokenKind.Ine or TokenKind.Cne) {
            EventPredicateField field = builder.Field(rightField!);
            if (field.ValueType != typeof(string) &&
                typeof(System.Collections.IEnumerable).IsAssignableFrom(field.ValueType)) {
                throw Unsupported(
                    $"Field-right -eq/-ne treats collection field '{field.Name}' as one PowerShell value. " +
                    "Use 'value' -in $_.Field or 'value' -notin $_.Field for collection membership.");
            }
        }
        if (fieldOnLeft && builder != null && binary.Operator is
                TokenKind.Iin or TokenKind.Cin or
                TokenKind.Inotin or TokenKind.Cnotin) {
            EventPredicateField field = builder.Field(leftField!);
            if (field.ValueType != typeof(string) &&
                typeof(System.Collections.IEnumerable).IsAssignableFrom(field.ValueType)) {
                throw Unsupported(
                    $"Field-left -in/-notin treats collection field '{field.Name}' as one PowerShell value. " +
                    "Use 'value' -in $_.Field for collection membership, or use the typed field Contains method.");
            }
        }
        ExpressionAst valueAst = fieldOnLeft ? binary.Right : binary.Left;
        object?[] values = ReadValues(valueAst);
        if (fieldOnLeft && builder != null && binary.Operator is
                TokenKind.Ieq or TokenKind.Ceq or TokenKind.Ine or TokenKind.Cne &&
            values.Length == 1 && values[0] == null) {
            EventPredicateField field = builder.Field(leftField!);
            if (field.ValueType != typeof(string) &&
                typeof(System.Collections.IEnumerable).IsAssignableFrom(field.ValueType)) {
                throw Unsupported(
                    $"PowerShell -eq/-ne against $null emits collection elements for field '{field.Name}' " +
                    "instead of testing whether the collection reference is null. " +
                    "Use the discoverable typed field IsNull or IsNotNull method.");
            }
        }
        if (fieldOnRight && binary.Operator is
                TokenKind.Iin or TokenKind.Cin or TokenKind.Inotin or TokenKind.Cnotin &&
            values.Length != 1) {
            throw Unsupported(
                "Field-right -in/-notin requires one scalar value on the left. " +
                "Use separate comparisons joined by -or when selecting more than one collection member.");
        }
        EventPredicateOperator comparison = ResolveOperator(binary.Operator, reverse: fieldOnRight);
        if (comparison is EventPredicateOperator.IsNull or EventPredicateOperator.IsNotNull) {
            return EventPredicate.Compare(fieldOnLeft ? leftField! : rightField!, comparison);
        }
        EventPredicate predicate = EventPredicate.Compare(
            fieldOnLeft ? leftField! : rightField!,
            comparison,
            values);
        predicate.IgnoreCase = IsCaseInsensitive(binary.Operator);
        return binary.Operator is TokenKind.Inotlike or TokenKind.Cnotlike or
            TokenKind.Inotmatch or TokenKind.Cnotmatch or
            TokenKind.Inotcontains or TokenKind.Cnotcontains
            ? EventPredicate.Not(predicate)
            : predicate;
    }

    private static bool IsCaseInsensitive(TokenKind token) => token switch {
        TokenKind.Ceq or TokenKind.Cne or TokenKind.Cin or TokenKind.Cnotin or
        TokenKind.Clike or TokenKind.Cnotlike or TokenKind.Cmatch or TokenKind.Cnotmatch or
        TokenKind.Cgt or TokenKind.Cge or TokenKind.Clt or TokenKind.Cle or
        TokenKind.Ccontains or TokenKind.Cnotcontains => false,
        _ => true
    };

    private static bool TryReadField(ExpressionAst expression, out string? field) {
        expression = Unwrap(expression);
        if (expression is MemberExpressionAst member &&
            member.Expression is VariableExpressionAst variable &&
            string.Equals(variable.VariablePath.UserPath, "_", StringComparison.Ordinal) &&
            member.Member is StringConstantExpressionAst name &&
            !member.Static) {
            field = name.Value;
            return true;
        }
        field = null;
        return false;
    }

    private static object?[] ReadValues(ExpressionAst expression) {
        expression = Unwrap(expression);
        if (expression is ArrayLiteralAst array) {
            return array.Elements.Select(ReadValue).ToArray();
        }
        if (expression is ArrayExpressionAst arrayExpression) {
            var values = new List<object?>();
            foreach (StatementAst statement in arrayExpression.SubExpression.Statements) {
                if (statement is not PipelineAst pipeline ||
                    pipeline.PipelineElements.Count != 1 ||
                    pipeline.PipelineElements[0] is not CommandExpressionAst command) {
                    throw Unsupported(
                        $"Array expression '{expression.Extent.Text}' can contain only literal values.");
                }
                ExpressionAst nestedExpression = Unwrap(command.Expression);
                if (nestedExpression is ArrayLiteralAst nested) {
                    values.AddRange(nested.Elements.Select(ReadValue));
                } else {
                    values.Add(ReadValue(nestedExpression));
                }
            }
            return values.ToArray();
        }
        return new[] { ReadValue(expression) };
    }

    private static object? ReadValue(ExpressionAst expression) {
        expression = Unwrap(expression);
        if (expression is ConstantExpressionAst constant) {
            return constant.Value;
        }
        if (expression is StringConstantExpressionAst text) {
            return text.Value;
        }
        if (expression is UnaryExpressionAst unary &&
            unary.TokenKind is TokenKind.Minus or TokenKind.Plus) {
            return ApplyNumericSign(unary);
        }
        if (expression is VariableExpressionAst variable) {
            return variable.VariablePath.UserPath.ToLowerInvariant() switch {
                "null" => null,
                "true" => true,
                "false" => false,
                _ => throw Unsupported(
                    $"Variable '${variable.VariablePath.UserPath}' is not allowed in Where. " +
                    "Use literal values or the discoverable New-EVXFilter builder.")
            };
        }
        throw Unsupported(
            $"Value expression '{expression.Extent.Text}' is not a literal. " +
            "Where is parsed and never executed.");
    }

    private static object ApplyNumericSign(UnaryExpressionAst unary) {
        ExpressionAst child = Unwrap(unary.Child);
        if (child is not ConstantExpressionAst constant || !IsNumeric(constant.Value)) {
            throw Unsupported(
                $"Unary expression '{unary.Extent.Text}' can be applied only to a numeric literal.");
        }
        if (unary.TokenKind == TokenKind.Plus) {
            return constant.Value;
        }
        try {
            return constant.Value switch {
                sbyte value => -value,
                byte value => -value,
                short value => -value,
                ushort value => -value,
                int value => checked(-value),
                uint value => -(long)value,
                long value => checked(-value),
                ulong value => -(decimal)value,
                float value => -value,
                double value => -value,
                decimal value => -value,
                _ => throw Unsupported($"Value '{unary.Extent.Text}' is not a supported numeric literal.")
            };
        } catch (OverflowException) {
            throw Unsupported($"Numeric literal '{unary.Extent.Text}' is outside the supported range.");
        }
    }

    private static bool IsNumeric(object value) => value is
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static EventPredicateOperator ResolveOperator(TokenKind token, bool reverse) {
        return (token, reverse) switch {
            (TokenKind.Ieq or TokenKind.Ceq, false) => EventPredicateOperator.Equal,
            (TokenKind.Ine or TokenKind.Cne, false) => EventPredicateOperator.NotEqual,
            (TokenKind.Iin or TokenKind.Cin, false) => EventPredicateOperator.In,
            (TokenKind.Inotin or TokenKind.Cnotin, false) => EventPredicateOperator.NotIn,
            (TokenKind.Iin or TokenKind.Cin, true) => EventPredicateOperator.In,
            (TokenKind.Inotin or TokenKind.Cnotin, true) => EventPredicateOperator.NotIn,
            (TokenKind.Ilike or TokenKind.Clike, false) => EventPredicateOperator.MatchesWildcard,
            (TokenKind.Inotlike or TokenKind.Cnotlike, false) => EventPredicateOperator.MatchesWildcard,
            (TokenKind.Imatch or TokenKind.Cmatch, false) => EventPredicateOperator.MatchesRegex,
            (TokenKind.Inotmatch or TokenKind.Cnotmatch, false) => EventPredicateOperator.MatchesRegex,
            (TokenKind.Igt or TokenKind.Cgt, false) => EventPredicateOperator.GreaterThan,
            (TokenKind.Ige or TokenKind.Cge, false) => EventPredicateOperator.GreaterThanOrEqual,
            (TokenKind.Ilt or TokenKind.Clt, false) => EventPredicateOperator.LessThan,
            (TokenKind.Ile or TokenKind.Cle, false) => EventPredicateOperator.LessThanOrEqual,
            (TokenKind.Icontains or TokenKind.Ccontains, false) => EventPredicateOperator.Equal,
            (TokenKind.Inotcontains or TokenKind.Cnotcontains, false) => EventPredicateOperator.Equal,
            (TokenKind.Ieq or TokenKind.Ceq, true) => EventPredicateOperator.Equal,
            (TokenKind.Ine or TokenKind.Cne, true) => EventPredicateOperator.NotEqual,
            (TokenKind.Igt or TokenKind.Cgt, true) => EventPredicateOperator.LessThan,
            (TokenKind.Ige or TokenKind.Cge, true) => EventPredicateOperator.LessThanOrEqual,
            (TokenKind.Ilt or TokenKind.Clt, true) => EventPredicateOperator.GreaterThan,
            (TokenKind.Ile or TokenKind.Cle, true) => EventPredicateOperator.GreaterThanOrEqual,
            _ => throw Unsupported($"Operator '{token}' is not supported by typed Where filters.")
        };
    }

    private static ExpressionAst Unwrap(ExpressionAst expression) {
        while (expression is ParenExpressionAst { Pipeline: PipelineAst pipeline } &&
               pipeline.PipelineElements.Count == 1 &&
               pipeline.PipelineElements[0] is CommandExpressionAst command) {
            expression = command.Expression;
        }
        return expression;
    }

    private static PSArgumentException Unsupported(string message) => new(
        message + " Supported operators are -eq, -ne, -in, -notin, -like, -match, " +
        "-contains, -notcontains, -gt, -ge, -lt, -le, -and, -or, and -not, " +
        "including their case-sensitive c-prefixed forms.");
}
