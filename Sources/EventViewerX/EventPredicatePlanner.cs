using System.Globalization;

namespace EventViewerX;

/// <summary>Splits reusable typed predicates into safe native and managed stages.</summary>
public static class EventPredicatePlanner {
    /// <summary>Plans a predicate for built-in typed records.</summary>
    public static EventPredicatePlan Plan(EventPredicate predicate) {
        return Plan(predicate, allowNative: true, managedReason: null);
    }

    /// <summary>Plans every predicate node for managed evaluation with an explicit host constraint.</summary>
    public static EventPredicatePlan PlanManaged(
        EventPredicate predicate,
        string reason) {

        return Plan(predicate, allowNative: false, managedReason: reason);
    }

    internal static EventPredicatePlan PlanManagedOnly(
        EventPredicate predicate,
        string reason) => PlanManaged(predicate, reason);

    private static EventPredicatePlan Plan(
        EventPredicate predicate,
        bool allowNative,
        string? managedReason) {

        if (predicate == null) {
            throw new ArgumentNullException(nameof(predicate));
        }
        predicate.Validate();
        EventPredicate snapshot = predicate.Clone();
        var steps = new List<EventPredicatePlanStep>();
        var native = new EventFilter();
        bool nativeApplied = false;
        EventPredicate? managed = PlanNode(
            snapshot,
            native,
            steps,
            allowNative,
            managedReason,
            ref nativeApplied);
        if (nativeApplied) {
            managed = snapshot;
            if (!native.HasAny) {
                steps.RemoveAll(static step => step.Stage == EventPredicatePlanStage.Native);
                steps.Add(new EventPredicatePlanStep(
                    "Contradictory native predicates",
                    EventPredicatePlanStage.Managed,
                    "Native equality intersections are empty, so the complete predicate is evaluated without a native prefilter."));
            }
            steps.Add(new EventPredicatePlanStep(
                "Exact predicate verification",
                EventPredicatePlanStage.Managed,
                "Native selection is a prefilter; the complete predicate is verified against the normalized typed record."));
        }
        return new EventPredicatePlan(
            native.HasAny ? native : null,
            managed,
            steps);
    }

    private static EventPredicate? PlanNode(
        EventPredicate predicate,
        EventFilter native,
        List<EventPredicatePlanStep> steps,
        bool allowNative,
        string? managedReasonOverride,
        ref bool nativeApplied) {

        if (predicate.Kind == EventPredicateKind.All) {
            var managedChildren = new List<EventPredicate>();
            foreach (EventPredicate child in predicate.Children) {
                EventPredicate? managed = PlanNode(
                    child,
                    native,
                    steps,
                    allowNative,
                    managedReasonOverride,
                    ref nativeApplied);
                if (managed != null) {
                    managedChildren.Add(managed);
                }
            }
            return managedChildren.Count switch {
                0 => null,
                1 => managedChildren[0],
                _ => EventPredicate.AllOf(managedChildren.ToArray())
            };
        }

        if (allowNative && predicate.Kind == EventPredicateKind.Comparison &&
            TryApplyNative(predicate, native, out string reason)) {
            nativeApplied = true;
            steps.Add(new EventPredicatePlanStep(
                Format(predicate),
                EventPredicatePlanStage.Native,
                reason));
            return null;
        }

        string managedReason = managedReasonOverride ?? (predicate.Kind == EventPredicateKind.Comparison
            ? "The field or operator requires the typed projected value."
            : "Boolean Any/Not groups retain exact semantics after typed projection.");
        steps.Add(new EventPredicatePlanStep(
            Format(predicate),
            EventPredicatePlanStage.Managed,
            managedReason));
        return predicate;
    }

    private static bool TryApplyNative(
        EventPredicate predicate,
        EventFilter native,
        out string reason) {

        string field = predicate.Field!;
        if (IsField(field, "EventId", "Id") &&
            TryReadIntSet(predicate, out int[]? eventIds)) {
            native.EventIds = Intersect(native.EventIds, eventIds!);
            reason = "Event ID equality is a native System predicate.";
            return true;
        }
        if (IsField(field, "RecordId", "EventRecordId") &&
            TryReadLongSet(predicate, out long[]? recordIds)) {
            native.RecordIds = Intersect(native.RecordIds, recordIds!);
            reason = "Record ID equality is a native System predicate.";
            return true;
        }
        if (IsField(field, "ProviderName", "Provider") &&
            !predicate.IgnoreCase &&
            TryReadStringSet(predicate, out string[]? providers)) {
            native.ProviderNames = Intersect(native.ProviderNames, providers!, StringComparer.Ordinal);
            reason = "Exact-case provider equality is a native System predicate.";
            return true;
        }
        if (IsField(field, "Level") &&
            TryReadByteSet(predicate, out byte[]? levels)) {
            native.Levels = Intersect(native.Levels, levels!);
            reason = "Level equality is a native System predicate.";
            return true;
        }
        if (IsField(field, "TimeCreated") &&
            predicate.Values.Count == 1 &&
            DateTime.TryParse(
                predicate.Values[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime time)) {
            time = time.ToUniversalTime();
            if (predicate.Operator is EventPredicateOperator.GreaterThan or EventPredicateOperator.GreaterThanOrEqual) {
                native.StartTime = !native.StartTime.HasValue || time > native.StartTime.Value
                    ? time
                    : native.StartTime;
                reason = "The lower time boundary is pushed into the native query.";
                return true;
            }
            if (predicate.Operator is EventPredicateOperator.LessThan or EventPredicateOperator.LessThanOrEqual) {
                native.EndTime = !native.EndTime.HasValue || time < native.EndTime.Value
                    ? time
                    : native.EndTime;
                reason = "The upper time boundary is pushed into the native query.";
                return true;
            }
        }
        reason = string.Empty;
        return false;
    }

    private static bool TryReadIntSet(EventPredicate predicate, out int[]? values) =>
        TryReadSet(predicate, static value => int.Parse(value!, CultureInfo.InvariantCulture), out values);

    private static bool TryReadLongSet(EventPredicate predicate, out long[]? values) =>
        TryReadSet(predicate, static value => long.Parse(value!, CultureInfo.InvariantCulture), out values);

    private static bool TryReadByteSet(EventPredicate predicate, out byte[]? values) =>
        TryReadSet(predicate, static value => {
            if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte numeric)) {
                return numeric;
            }
            return checked((byte)(Level)Enum.Parse(typeof(Level), value!, ignoreCase: true));
        }, out values);

    private static bool TryReadStringSet(EventPredicate predicate, out string[]? values) =>
        TryReadSet(predicate, static value => value ?? string.Empty, out values);

    private static bool TryReadSet<T>(
        EventPredicate predicate,
        Func<string?, T> convert,
        out T[]? values) {

        values = null;
        if (predicate.Operator is not EventPredicateOperator.Equal and not EventPredicateOperator.In ||
            predicate.Values.Count == 0 || predicate.Values.Any(static value => value == null)) {
            return false;
        }
        try {
            values = predicate.Values.Select(convert).Distinct().ToArray();
            return values.Length > 0;
        } catch (ArgumentException) {
            return false;
        } catch (FormatException) {
            return false;
        } catch (OverflowException) {
            return false;
        }
    }

    private static IReadOnlyList<T> Intersect<T>(
        IReadOnlyList<T>? existing,
        IReadOnlyList<T> incoming,
        IEqualityComparer<T>? comparer = null) {

        comparer ??= EqualityComparer<T>.Default;
        return existing == null
            ? incoming.Distinct(comparer).ToArray()
            : existing.Intersect(incoming, comparer).ToArray();
    }

    private static bool IsField(string actual, params string[] candidates) =>
        candidates.Any(candidate => string.Equals(actual, candidate, StringComparison.OrdinalIgnoreCase));

    private static string Format(EventPredicate predicate) {
        if (predicate.Kind != EventPredicateKind.Comparison) {
            return $"{predicate.Kind}({predicate.Children.Count})";
        }
        string values = string.Join(", ", predicate.Values.Select(static value => value ?? "null"));
        return $"{predicate.Field} {predicate.Operator} {values}".TrimEnd();
    }
}
