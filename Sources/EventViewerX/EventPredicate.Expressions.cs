using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace EventViewerX;

public sealed partial class EventPredicate {
    /// <summary>
    /// Translates a restricted strongly typed C# expression into the shared serializable predicate model.
    /// Arbitrary method calls are rejected rather than executed.
    /// </summary>
    public static EventPredicate FromExpression<TRecord>(
        Expression<Func<TRecord, bool>> expression) where TRecord : EventTypeRecord {

        if (expression == null) {
            throw new ArgumentNullException(nameof(expression));
        }
        EventPredicate predicate = ExpressionTranslator.Translate(expression.Body, expression.Parameters[0]);
        predicate.Validate();
        return predicate;
    }

    private static class ExpressionTranslator {
        internal static EventPredicate Translate(Expression expression, ParameterExpression parameter) {
            expression = StripConvert(expression);
            if (expression is BinaryExpression binary) {
                return TranslateBinary(binary, parameter);
            }
            if (expression is UnaryExpression { NodeType: ExpressionType.Not } unary) {
                return Not(Translate(unary.Operand, parameter));
            }
            if (expression is MethodCallExpression call) {
                return TranslateCall(call, parameter);
            }
            throw Unsupported(expression);
        }

        private static EventPredicate TranslateBinary(BinaryExpression binary, ParameterExpression parameter) {
            if (binary.NodeType == ExpressionType.AndAlso) {
                return AllOf(Translate(binary.Left, parameter), Translate(binary.Right, parameter));
            }
            if (binary.NodeType == ExpressionType.OrElse) {
                return AnyOf(Translate(binary.Left, parameter), Translate(binary.Right, parameter));
            }

            bool memberOnLeft = TryGetField(binary.Left, parameter, out string? field);
            bool memberOnRight = TryGetField(binary.Right, parameter, out string? rightField);
            if (memberOnLeft == memberOnRight) {
                throw Unsupported(binary);
            }
            Expression valueExpression = memberOnLeft ? binary.Right : binary.Left;
            object? value = ReadCapturedValue(valueExpression);
            if (value == null && binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual) {
                EventPredicate nullPredicate = Compare(
                    memberOnLeft ? field! : rightField!,
                    binary.NodeType == ExpressionType.Equal
                        ? EventPredicateOperator.IsNull
                        : EventPredicateOperator.IsNotNull);
                nullPredicate.IgnoreCase = false;
                return nullPredicate;
            }
            Expression fieldExpression = memberOnLeft ? binary.Left : binary.Right;
            if (binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual &&
                IsCollectionType(StripConvert(fieldExpression).Type)) {
                throw new NotSupportedException(
                    $"Non-null reference comparison for collection field '{(memberOnLeft ? field : rightField)}' " +
                    "cannot be represented by EventPredicate. Compare the collection to null or use Contains.");
            }
            EventPredicateOperator comparison = ToOperator(binary.NodeType, reverse: memberOnRight);
            return CompareExpression(memberOnLeft ? field! : rightField!, comparison, value);
        }

        private static EventPredicate TranslateCall(MethodCallExpression call, ParameterExpression parameter) {
            if (call.Object != null && TryGetField(call.Object, parameter, out string? field)) {
                EventPredicateOperator comparison = call.Method.Name switch {
                    nameof(string.Contains) => EventPredicateOperator.Contains,
                    nameof(string.StartsWith) => EventPredicateOperator.StartsWith,
                    nameof(string.EndsWith) => EventPredicateOperator.EndsWith,
                    _ => throw Unsupported(call)
                };
                if (call.Arguments.Count != 1) {
                    throw Unsupported(call);
                }
                return CompareExpression(field!, comparison, ReadCapturedValue(call.Arguments[0]));
            }

            Expression? collectionExpression = null;
            Expression? memberExpression = null;
            Expression? comparerExpression = null;
            if (string.Equals(call.Method.Name, nameof(Enumerable.Contains), StringComparison.Ordinal)) {
                if (call.Object != null && call.Arguments.Count == 1) {
                    collectionExpression = call.Object;
                    memberExpression = call.Arguments[0];
                } else if (call.Object == null && call.Arguments.Count == 2) {
                    collectionExpression = call.Arguments[0];
                    memberExpression = call.Arguments[1];
                } else if (call.Object == null && call.Arguments.Count == 3) {
                    collectionExpression = call.Arguments[0];
                    memberExpression = call.Arguments[1];
                    comparerExpression = call.Arguments[2];
                }
            }
            if (collectionExpression != null && memberExpression != null &&
                TryGetField(memberExpression, parameter, out string? inField)) {
                if (IsCollectionType(StripConvert(memberExpression).Type)) {
                    throw new NotSupportedException(
                        $"Outer collection Contains for collection field '{inField}' cannot be represented by " +
                        "EventPredicate because it depends on collection reference or comparer semantics. " +
                        "Compare a scalar value to the field elements instead.");
                }
                object? collection = ReadCapturedValue(collectionExpression);
                if (collection is not IEnumerable values || collection is string) {
                    throw Unsupported(call);
                }
                var items = new List<object?>();
                foreach (object? item in values) {
                    items.Add(item);
                }
                bool ignoreCase = comparerExpression != null
                    ? ResolveComparerIgnoreCase(
                        ReadCapturedValue(comparerExpression),
                        StripConvert(memberExpression).Type,
                        call)
                    : ResolveCollectionIgnoreCase(
                        collection,
                        StripConvert(memberExpression).Type,
                        call.Method.DeclaringType == typeof(Enumerable),
                        call);
                return CompareExpressionValues(
                    inField!,
                    EventPredicateOperator.In,
                    items,
                    ignoreCase);
            }
            throw Unsupported(call);
        }

        private static EventPredicate CompareExpression(
            string field,
            EventPredicateOperator comparison,
            object? value) {

            EventPredicate predicate = Compare(field, comparison, value);
            predicate.IgnoreCase = false;
            return predicate;
        }

        private static EventPredicate CompareExpressionValues(
            string field,
            EventPredicateOperator comparison,
            IReadOnlyCollection<object?> values,
            bool ignoreCase = false) {

            EventPredicate predicate = Compare(field, comparison, values.ToArray());
            predicate.IgnoreCase = ignoreCase;
            return predicate;
        }

        private static bool ResolveCollectionIgnoreCase(
            object collection,
            Type elementType,
            bool enumerableContains,
            Expression expression) {

            Type collectionType = collection.GetType();
            if (collectionType.IsArray) {
                return false;
            }
            if (collectionType.IsGenericType) {
                Type genericType = collectionType.GetGenericTypeDefinition();
                if (genericType == typeof(HashSet<>)) {
                    object? comparer = collectionType.GetProperty(nameof(HashSet<int>.Comparer))
                        ?.GetValue(collection, null);
                    return ResolveComparerIgnoreCase(comparer, elementType, expression);
                }
                if (genericType == typeof(List<>) ||
                    genericType == typeof(Queue<>) ||
                    genericType == typeof(Stack<>) ||
                    genericType == typeof(LinkedList<>) ||
                    genericType == typeof(System.Collections.ObjectModel.Collection<>) ||
                    genericType == typeof(System.Collections.ObjectModel.ReadOnlyCollection<>)) {
                    return false;
                }
            }
            Type collectionInterface = typeof(ICollection<>).MakeGenericType(elementType);
            if (enumerableContains && !collectionInterface.IsAssignableFrom(collectionType)) {
                return false;
            }
            throw new NotSupportedException(
                $"Collection comparison semantics for '{collectionType.FullName}' cannot be represented by " +
                "EventPredicate. Use an array, List<T>, or HashSet<T> with the default, ordinal, or ordinal-ignore-case comparer.");
        }

        private static bool ResolveComparerIgnoreCase(
            object? comparer,
            Type elementType,
            Expression expression) {

            if (comparer == null) {
                return false;
            }
            Type equalityComparerType = typeof(EqualityComparer<>).MakeGenericType(elementType);
            object? defaultComparer = equalityComparerType.GetProperty(
                nameof(EqualityComparer<int>.Default),
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
            if (ReferenceEquals(comparer, defaultComparer) || Equals(comparer, defaultComparer)) {
                return false;
            }
            if (elementType == typeof(string)) {
                if (ReferenceEquals(comparer, StringComparer.Ordinal)) {
                    return false;
                }
                if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            throw new NotSupportedException(
                $"Comparer '{comparer.GetType().FullName}' in expression '{expression}' cannot be represented by " +
                "EventPredicate. Use the default comparer, StringComparer.Ordinal, or StringComparer.OrdinalIgnoreCase.");
        }

        private static bool IsCollectionType(Type type) =>
            type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

        private static bool TryGetField(
            Expression expression,
            ParameterExpression parameter,
            out string? field) {

            expression = StripConvert(expression);
            if (expression is MemberExpression member &&
                StripConvert(member.Expression!) == parameter) {
                field = member.Member.Name;
                return true;
            }
            field = null;
            return false;
        }

        private static object? ReadCapturedValue(Expression expression) {
            expression = StripConvert(expression);
            if (expression is MethodCallExpression {
                    Method.Name: "op_Implicit",
                    Arguments.Count: 1
                } conversion) {
                return ReadCapturedValue(conversion.Arguments[0]);
            }
            if (expression is ConstantExpression constant) {
                return constant.Value;
            }
            if (expression is NewArrayExpression array) {
                return array.Expressions.Select(ReadCapturedValue).ToArray();
            }
            if (expression is MemberExpression member) {
                object? owner = member.Expression == null ? null : ReadCapturedValue(member.Expression);
                return member.Member switch {
                    FieldInfo field => field.GetValue(owner),
                    PropertyInfo property when property.GetMethod != null && property.GetMethod.GetParameters().Length == 0 =>
                        property.GetValue(owner, null),
                    _ => throw Unsupported(expression)
                };
            }
            throw Unsupported(expression);
        }

        private static EventPredicateOperator ToOperator(ExpressionType type, bool reverse) {
            return (type, reverse) switch {
                (ExpressionType.Equal, _) => EventPredicateOperator.Equal,
                (ExpressionType.NotEqual, _) => EventPredicateOperator.NotEqual,
                (ExpressionType.GreaterThan, false) => EventPredicateOperator.GreaterThan,
                (ExpressionType.GreaterThanOrEqual, false) => EventPredicateOperator.GreaterThanOrEqual,
                (ExpressionType.LessThan, false) => EventPredicateOperator.LessThan,
                (ExpressionType.LessThanOrEqual, false) => EventPredicateOperator.LessThanOrEqual,
                (ExpressionType.GreaterThan, true) => EventPredicateOperator.LessThan,
                (ExpressionType.GreaterThanOrEqual, true) => EventPredicateOperator.LessThanOrEqual,
                (ExpressionType.LessThan, true) => EventPredicateOperator.GreaterThan,
                (ExpressionType.LessThanOrEqual, true) => EventPredicateOperator.GreaterThanOrEqual,
                _ => throw new NotSupportedException($"Expression operator '{type}' is not supported by EventPredicate.")
            };
        }

        private static Expression StripConvert(Expression expression) {
            while (expression is UnaryExpression unary &&
                   unary.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked) {
                expression = unary.Operand;
            }
            return expression;
        }

        private static NotSupportedException Unsupported(Expression expression) => new(
            $"Expression node '{expression.NodeType}' ({expression}) is not supported. " +
            "Use property comparisons, &&, ||, !, string Contains/StartsWith/EndsWith, or collection Contains.");
    }
}
