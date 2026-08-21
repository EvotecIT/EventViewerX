using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.QueryHelpers;

internal static class QueryValidationHelpers {
    internal static bool IsNegative(int value) => value < 0;

    internal static bool IsNegative(long value) => value < 0;

    internal static bool IsNonPositiveWhenProvided(int? value) => value.HasValue && value.Value <= 0;

    internal static bool HasInvalidUtcRange(DateTime? startUtc, DateTime? endUtc) =>
        startUtc.HasValue && endUtc.HasValue && startUtc.Value > endUtc.Value;

    internal static bool HasNonPositiveValues(IReadOnlyList<int>? values) {
        if (values is null) {
            return false;
        }

        for (var i = 0; i < values.Count; i++) {
            if (values[i] <= 0) {
                return true;
            }
        }

        return false;
    }
}
