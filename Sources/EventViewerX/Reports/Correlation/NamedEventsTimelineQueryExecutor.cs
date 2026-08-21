using System.Globalization;
using System.Security.Cryptography;
using EventViewerX.Reports.QueryHelpers;

namespace EventViewerX.Reports.Correlation;

/// <summary>
/// Builds timeline and correlation projections for named-event detections.
/// </summary>
internal static partial class NamedEventsTimelineQueryExecutor {
    private const int MaxCorrelationKeys = 8;
    private const int MaxPayloadKeys = 64;
    private const int MaxGroupsCap = 2000;
    private const int MaxBucketMinutes = 1440;
    private const int MaxThreadsCap = 8;
    private const int CorrelationIdHashBytes = 8;
    private const int CorrelationTokenMinimumCapacity = 16;
    private const string HexSeparator = "-";
    private static readonly string[] AllowedCorrelationKeysValue = {
        "who",
        "object_affected",
        "computer",
        "action",
        "named_event",
        "event_id",
        "gathered_from",
        "gathered_log_name"
    };

    private static readonly string[] DefaultCorrelationKeysValue = {
        "who",
        "object_affected",
        "computer"
    };

    /// <summary>
    /// Allowed correlation dimensions accepted by <see cref="TryBuildAsync"/>.
    /// </summary>
    public static IReadOnlyList<string> AllowedCorrelationKeys => AllowedCorrelationKeysValue;

    /// <summary>
    /// Default correlation dimensions used when no explicit dimensions are provided.
    /// </summary>
    public static IReadOnlyList<string> DefaultCorrelationKeys => DefaultCorrelationKeysValue;

    /// <summary>
    /// Builds timeline and correlation projections from a named-events query request.
    /// </summary>
    /// <param name="request">Query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple containing either a successful <see cref="NamedEventsTimelineQueryResult"/> or a
    /// <see cref="NamedEventsTimelineQueryFailure"/>.
    /// </returns>
    public static async Task<(NamedEventsTimelineQueryResult? Result, NamedEventsTimelineQueryFailure? Failure)> TryBuildAsync(
        NamedEventsTimelineQueryRequest request,
        CancellationToken cancellationToken = default) {
        if (request is null) {
            return (null, new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "request is required."
            });
        }

        if (!TryValidateRequest(
                request,
                out var normalizedNamedEvents,
                out var normalizedMachines,
                out var normalizedCorrelationKeys,
                out var normalizedPayloadKeys,
                out var normalizedEventIds,
                out var failure)) {
            return (null, failure);
        }

        var maxEvents = request.MaxEvents <= 0 ? 1 : request.MaxEvents;
        var maxThreads = Math.Max(1, Math.Min(request.MaxThreads <= 0 ? 4 : request.MaxThreads, MaxThreadsCap));
        var maxGroups = Math.Max(1, Math.Min(request.MaxGroups <= 0 ? 250 : request.MaxGroups, MaxGroupsCap));
        var bucketMinutes = Math.Max(1, Math.Min(request.BucketMinutes <= 0 ? 15 : request.BucketMinutes, MaxBucketMinutes));
        var maxEventsPerNamedEvent = request.MaxEventsPerNamedEvent.HasValue && request.MaxEventsPerNamedEvent.Value > 0
            ? request.MaxEventsPerNamedEvent
            : null;
        var effectiveNamedEvents = normalizedNamedEvents ?? new List<EventType>();
        var includeUncorrelated = request.IncludeUncorrelated;
        var includePayload = request.IncludePayload;
        string? logName = null;
        var requestLogName = request.LogName;
        var trimmedLogName = requestLogName is null ? string.Empty : requestLogName.Trim();
        if (trimmedLogName.Length > 0) {
            logName = trimmedLogName;
        }

        var rows = new List<EventRowAccumulator>(Math.Min(maxEvents, 256));
        var perNamedEventCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var truncatedNamedEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filteredOut = 0;
        var filteredUncorrelated = 0;
        var outputTruncated = false;
        var queryInfo = new EventTypeQueryExecutionInfo();
        var selectedRows = new Dictionary<EventObject, EventRowAccumulator>();
        int selectionLimit = maxEvents == int.MaxValue ? int.MaxValue : maxEvents + 1;

        bool TrySelectTimelineEvent(EventTypeRecord item) {
            var namedEventName = ResolveTypeName(item);
            var row = ToAccumulator(item, namedEventName, includePayload, normalizedPayloadKeys);
            var correlation = BuildCorrelationValues(row, normalizedCorrelationKeys);
            row.Correlation = correlation;
            var hasCorrelation = correlation.Values.Any(static value => !string.IsNullOrWhiteSpace(value));
            if (!hasCorrelation && !includeUncorrelated) {
                filteredUncorrelated++;
                return false;
            }

            if (maxEventsPerNamedEvent.HasValue) {
                var current = perNamedEventCount.TryGetValue(namedEventName, out var count) ? count : 0;
                if (current >= maxEventsPerNamedEvent.Value) {
                    truncatedNamedEvents.Add(namedEventName);
                    filteredOut++;
                    return false;
                }
            }

            selectedRows[item.SourceEvent] = row;
            perNamedEventCount[namedEventName] = perNamedEventCount.TryGetValue(namedEventName, out var existingCount)
                ? existingCount + 1
                : 1;
            return true;
        }

        try {
            var namedQuery =
                new EventTypeQuery(
                    effectiveNamedEvents) {
                    MachineNames =
                        normalizedMachines.Count > 0
                            ? normalizedMachines
                                .Cast<string?>()
                                .ToArray()
                            : null,
                    StartTime =
                        request.StartTimeUtc,
                    EndTime =
                        request.EndTimeUtc,
                    TimePeriod =
                        request.TimePeriod,
                    MaxConcurrency = maxThreads,
                    MaxEvents = selectionLimit,
                    MaxCandidates =
                        request.MaxEventsScanned,
                    ResultPredicate =
                        TrySelectTimelineEvent,
                    SourceLogName = logName,
                    SourceEventIds =
                        normalizedEventIds
                };
            await foreach (var item in
                           EventTypeEngine.ReadAsync(
                               namedQuery,
                               queryInfo,
                               cancellationToken)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (rows.Count >= maxEvents) {
                    outputTruncated = true;
                    break;
                }
                if (selectedRows.TryGetValue(item.SourceEvent, out EventRowAccumulator? row)) {
                    selectedRows.Remove(item.SourceEvent);
                    rows.Add(row);
                }
            }
        } catch (OperationCanceledException) {
            throw;
        } catch (ArgumentException ex) {
            return (null, new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = ex.Message
            });
        } catch (InvalidOperationException ex) {
            return (null, new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.QueryFailed,
                Message = ex.Message
            });
        } catch (Exception ex) {
            return (null, new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.Exception,
                Message = ex.Message
            });
        }

        var orderedRows = rows
            .OrderBy(static row => row.WhenUtcDate ?? DateTime.MaxValue)
            .ThenBy(static row => row.RecordId ?? long.MaxValue)
            .ThenBy(static row => row.EventType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var timelineRows = new List<NamedEventsTimelineEventRow>(orderedRows.Count);
        var groups = new Dictionary<string, GroupAccumulator>(StringComparer.OrdinalIgnoreCase);
        var buckets = new Dictionary<DateTime, BucketAccumulator>();
        var sequence = 0;

        for (var i = 0; i < orderedRows.Count; i++) {
            var row = orderedRows[i];
            var correlationToken = BuildCorrelationToken(row.Correlation);
            var correlationId = BuildCorrelationId(correlationToken);

            sequence++;
            timelineRows.Add(new NamedEventsTimelineEventRow {
                Sequence = sequence,
                CorrelationId = correlationId,
                Correlation = row.Correlation,
                EventType = row.EventType,
                RuleType = row.RuleType,
                EventId = row.EventId,
                RecordId = row.RecordId,
                GatheredFrom = row.GatheredFrom,
                GatheredLogName = row.GatheredLogName,
                WhenUtc = row.WhenUtc,
                Who = row.Who,
                ObjectAffected = row.ObjectAffected,
                Computer = row.Computer,
                Action = row.Action,
                Payload = row.Payload
            });

            if (!groups.TryGetValue(correlationId, out var group)) {
                group = new GroupAccumulator {
                    CorrelationId = correlationId,
                    Correlation = row.Correlation
                };
                groups[correlationId] = group;
            }

            group.EventCount++;
            group.EventType.Add(row.EventType);
            group.EventIds.Add(row.EventId);
            if (!string.IsNullOrWhiteSpace(row.GatheredFrom)) {
                group.Machines.Add(row.GatheredFrom);
            }

            if (row.WhenUtcDate.HasValue) {
                if (!group.FirstSeenUtc.HasValue || row.WhenUtcDate.Value < group.FirstSeenUtc.Value) {
                    group.FirstSeenUtc = row.WhenUtcDate.Value;
                }
                if (!group.LastSeenUtc.HasValue || row.WhenUtcDate.Value > group.LastSeenUtc.Value) {
                    group.LastSeenUtc = row.WhenUtcDate.Value;
                }

                var bucketStart = FloorToBucket(row.WhenUtcDate.Value, bucketMinutes);
                if (!buckets.TryGetValue(bucketStart, out var bucket)) {
                    bucket = new BucketAccumulator {
                        BucketStartUtc = bucketStart
                    };
                    buckets[bucketStart] = bucket;
                }

                bucket.EventCount++;
                bucket.CorrelationIds.Add(correlationId);
            }
        }

        var orderedGroups = groups.Values
            .OrderByDescending(static group => group.EventCount)
            .ThenBy(static group => group.FirstSeenUtc ?? DateTime.MaxValue)
            .ThenBy(static group => group.CorrelationId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var groupsTotal = orderedGroups.Count;

        var groupsTruncated = orderedGroups.Count > maxGroups;
        if (groupsTruncated) {
            orderedGroups = orderedGroups.Take(maxGroups).ToList();
        }

        var groupRows = orderedGroups
            .Select(group => new NamedEventsTimelineGroupRow {
                CorrelationId = group.CorrelationId,
                Correlation = group.Correlation,
                EventCount = group.EventCount,
                FirstSeenUtc = group.FirstSeenUtc?.ToString("O"),
                LastSeenUtc = group.LastSeenUtc?.ToString("O"),
                DurationMinutes = group.FirstSeenUtc.HasValue && group.LastSeenUtc.HasValue
                    ? Math.Round((group.LastSeenUtc.Value - group.FirstSeenUtc.Value).TotalMinutes, 3)
                    : null,
                EventType = group.EventType.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                EventIds = group.EventIds.OrderBy(static value => value).ToArray(),
                Machines = group.Machines.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray()
            })
            .ToArray();

        var bucketRows = buckets.Values
            .OrderBy(static bucket => bucket.BucketStartUtc)
            .Select(bucket => new NamedEventsTimelineBucketRow {
                BucketStartUtc = bucket.BucketStartUtc.ToString("O"),
                BucketEndUtc = bucket.BucketStartUtc.AddMinutes(bucketMinutes).ToString("O"),
                EventCount = bucket.EventCount,
                CorrelationCount = bucket.CorrelationIds.Count
            })
            .ToArray();

        var result = new NamedEventsTimelineQueryResult {
            RequestedNamedEvents = effectiveNamedEvents
                .Select(static value => ToSnakeCase(value.ToString()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            EffectiveMachines = normalizedMachines,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            MaxEvents = maxEvents,
            MaxEventsScanned = request.MaxEventsScanned,
            EventsScanned = queryInfo.EventsScanned,
            MaxEventsPerNamedEvent = maxEventsPerNamedEvent,
            MaxThreads = maxThreads,
            CorrelationKeys = normalizedCorrelationKeys,
            IncludeUncorrelated = includeUncorrelated,
            BucketMinutes = bucketMinutes,
            Truncated = outputTruncated || queryInfo.ScanLimitReached || truncatedNamedEvents.Count > 0,
            OutputTruncated = outputTruncated,
            ScanTruncated = queryInfo.ScanLimitReached,
            PerNamedEventTruncated = truncatedNamedEvents.Count > 0,
            TruncatedNamedEvents = truncatedNamedEvents
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            GroupsTruncated = groupsTruncated,
            GroupsTotal = groupsTotal,
            FilteredOut = filteredOut,
            FilteredUncorrelated = filteredUncorrelated,
            Timeline = timelineRows,
            CorrelationGroups = groupRows,
            Buckets = bucketRows
        };
        ApplyTargetFailures(result, queryInfo);
        return (result, null);
    }

    internal static void ApplyTargetFailures(
        NamedEventsTimelineQueryResult result,
        EventTypeQueryExecutionInfo queryInfo) {
        if (result is null) {
            throw new ArgumentNullException(nameof(result));
        }
        if (queryInfo is null) {
            throw new ArgumentNullException(nameof(queryInfo));
        }

        IReadOnlyList<EventLogQueryTargetFailure> targetFailures = queryInfo.TargetFailures;
        result.TargetFailures = targetFailures;
        result.Incomplete = targetFailures.Count > 0;
        result.Truncated |= result.Incomplete;
    }

    private static bool TryValidateRequest(
        NamedEventsTimelineQueryRequest request,
        out List<EventType> normalizedNamedEvents,
        out List<string> normalizedMachines,
        out List<string> normalizedCorrelationKeys,
        out HashSet<string>? normalizedPayloadKeys,
        out HashSet<int>? normalizedEventIds,
        out NamedEventsTimelineQueryFailure? failure) {
        normalizedNamedEvents = new List<EventType>();
        normalizedMachines = new List<string>();
        normalizedCorrelationKeys = new List<string>();
        normalizedPayloadKeys = null;
        normalizedEventIds = null;

        if (request is null) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "request is required."
            };
            return false;
        }

        if (request.EventType is null || request.EventType.Count == 0) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "namedEvents must contain at least one value."
            };
            return false;
        }

        if (QueryValidationHelpers.HasInvalidUtcRange(request.StartTimeUtc, request.EndTimeUtc)) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "startTimeUtc must be less than or equal to endTimeUtc."
            };
            return false;
        }

        if (request.TimePeriod.HasValue && (request.StartTimeUtc.HasValue || request.EndTimeUtc.HasValue)) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "timePeriod cannot be combined with startTimeUtc/endTimeUtc."
            };
            return false;
        }

        if (request.MaxEvents <= 0) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "maxEvents must be greater than 0."
            };
            return false;
        }

        if (request.MaxEventsScanned < 0) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "maxEventsScanned must be greater than or equal to 0."
            };
            return false;
        }

        if (request.MaxThreads <= 0) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "maxThreads must be greater than 0."
            };
            return false;
        }

        if (request.MaxEventsPerNamedEvent.HasValue && request.MaxEventsPerNamedEvent.Value <= 0) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "maxEventsPerNamedEvent must be greater than 0 when provided."
            };
            return false;
        }

        if (request.MaxGroups <= 0) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "maxGroups must be greater than 0."
            };
            return false;
        }

        if (request.BucketMinutes <= 0 || request.BucketMinutes > MaxBucketMinutes) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = $"bucketMinutes must be between 1 and {MaxBucketMinutes}."
            };
            return false;
        }

        if (request.EventIds is not null && QueryValidationHelpers.HasNonPositiveValues(request.EventIds)) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = "eventIds must contain only positive values."
            };
            return false;
        }

        normalizedNamedEvents = request.EventType
            .Distinct(EqualityComparer<EventType>.Default)
            .ToList();

        if (request.MachineNames is not null) {
            for (var i = 0; i < request.MachineNames.Count; i++) {
                var candidate = request.MachineNames[i];
                if (string.IsNullOrWhiteSpace(candidate)) {
                    continue;
                }

                var trimmed = candidate.Trim();
                if (!normalizedMachines.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) {
                    normalizedMachines.Add(trimmed);
                }
            }
        }

        if (request.EventIds is not null && request.EventIds.Count > 0) {
            normalizedEventIds = request.EventIds
                .Distinct()
                .ToHashSet();
        }

        if (!TryNormalizeCorrelationKeys(request.CorrelationKeys, out normalizedCorrelationKeys, out var correlationError)) {
            failure = new NamedEventsTimelineQueryFailure {
                Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                Message = correlationError ?? "Invalid correlation keys."
            };
            return false;
        }

        if (request.PayloadKeys is not null && request.PayloadKeys.Count > 0) {
            if (request.PayloadKeys.Count > MaxPayloadKeys) {
                failure = new NamedEventsTimelineQueryFailure {
                    Kind = NamedEventsTimelineQueryFailureKind.InvalidArgument,
                    Message = $"payloadKeys supports at most {MaxPayloadKeys} values."
                };
                return false;
            }

            normalizedPayloadKeys = request.PayloadKeys
                .Select(ToSnakeCase)
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        failure = null;
        return true;
    }

    private static bool TryNormalizeCorrelationKeys(
        IReadOnlyList<string>? requested,
        out List<string> normalized,
        out string? error) {
        normalized = new List<string>();
        error = null;

        if (requested is null || requested.Count == 0) {
            normalized.AddRange(DefaultCorrelationKeysValue);
            return true;
        }

        if (requested.Count > MaxCorrelationKeys) {
            error = $"correlationKeys supports at most {MaxCorrelationKeys} values.";
            return false;
        }

        var allowed = new HashSet<string>(AllowedCorrelationKeysValue, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < requested.Count; i++) {
            var raw = requested[i];
            var key = ToSnakeCase(raw);
            if (string.IsNullOrWhiteSpace(key) || !allowed.Contains(key)) {
                error = $"correlationKeys[{i}] ('{raw}') is not recognized. Allowed values: {string.Join(", ", AllowedCorrelationKeysValue)}.";
                return false;
            }

            if (seen.Add(key)) {
                normalized.Add(key);
            }
        }

        if (normalized.Count == 0) {
            error = "correlationKeys must contain at least one valid value.";
            return false;
        }

        return true;
    }

    private static EventRowAccumulator ToAccumulator(
        EventTypeRecord item,
        string namedEvent,
        bool includePayload,
        HashSet<string>? payloadKeySet) {
        if (item is null) {
            throw new ArgumentNullException(nameof(item));
        }

        var fullPayload = ExtractPayload(item);
        var payload = includePayload
            ? ProjectPayload(fullPayload, payloadKeySet)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var whenUtc = ReadPayloadUtc(fullPayload, "when");

        return new EventRowAccumulator(
            namedEvent,
            item.GetType().Name,
            item.EventId,
            item.RecordId,
            item.MachineName,
            item.SourceLogName,
            whenUtc,
            ParseUtc(whenUtc),
            ReadPayloadString(fullPayload, "who"),
            ReadPayloadString(fullPayload, "object_affected"),
            ReadPayloadString(fullPayload, "computer"),
            ReadPayloadString(fullPayload, "action"),
            payload);
    }

    private static string ResolveTypeName(EventTypeRecord item) {
        return Enum.TryParse<EventType>(item.TypeName, out var parsedNamedEvent)
            ? ToSnakeCase(parsedNamedEvent.ToString())
            : ToSnakeCase(item.TypeName);
    }

    private static IReadOnlyDictionary<string, string> BuildCorrelationValues(
        EventRowAccumulator row,
        IReadOnlyList<string> correlationKeys) {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < correlationKeys.Count; i++) {
            var key = correlationKeys[i];
            values[key] = ResolveCorrelationValue(row, key);
        }

        return values;
    }

    private static string ResolveCorrelationValue(EventRowAccumulator row, string correlationKey) {
        return correlationKey switch {
            "who" => NormalizeCorrelationValue(row.Who),
            "object_affected" => NormalizeCorrelationValue(row.ObjectAffected),
            "computer" => NormalizeCorrelationValue(row.Computer),
            "action" => NormalizeCorrelationValue(row.Action),
            "named_event" => NormalizeCorrelationValue(row.EventType),
            "event_id" => row.EventId.ToString(CultureInfo.InvariantCulture),
            "gathered_from" => NormalizeCorrelationValue(row.GatheredFrom),
            "gathered_log_name" => NormalizeCorrelationValue(row.GatheredLogName),
            _ => string.Empty
        };
    }

    private static string NormalizeCorrelationValue(string? value) {
        if (value is null) {
            return string.Empty;
        }

        string nonNullValue = value;
        var trimmed = nonNullValue.Trim();
        return trimmed.Length == 0 ? string.Empty : trimmed;
    }

    private static string BuildCorrelationToken(IReadOnlyDictionary<string, string> correlation) {
        if (correlation.Count == 0) {
            return "uncorrelated";
        }

        var orderedKeys = correlation.Keys
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sb = new StringBuilder(CorrelationTokenMinimumCapacity);
        sb.Append(orderedKeys.Length.ToString(CultureInfo.InvariantCulture));
        sb.Append(':');
        for (var i = 0; i < orderedKeys.Length; i++) {
            var key = orderedKeys[i];
            var value = correlation[key];
            AppendLengthPrefixedValue(sb, key);
            AppendLengthPrefixedValue(sb, value);
        }

        return sb.ToString();
    }

    private static void AppendLengthPrefixedValue(StringBuilder builder, string value) {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
    }

    private static string BuildCorrelationId(string token) {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return BitConverter.ToString(hash, 0, CorrelationIdHashBytes).Replace(HexSeparator, string.Empty).ToLowerInvariant();
    }

    private sealed class EventRowAccumulator {
        public EventRowAccumulator(
            string namedEvent,
            string ruleType,
            int eventId,
            long? recordId,
            string gatheredFrom,
            string gatheredLogName,
            string? whenUtc,
            DateTime? whenUtcDate,
            string? who,
            string? objectAffected,
            string? computer,
            string? action,
            Dictionary<string, object?> payload) {
            EventType = namedEvent;
            RuleType = ruleType;
            EventId = eventId;
            RecordId = recordId;
            GatheredFrom = gatheredFrom;
            GatheredLogName = gatheredLogName;
            WhenUtc = whenUtc;
            WhenUtcDate = whenUtcDate;
            Who = who;
            ObjectAffected = objectAffected;
            Computer = computer;
            Action = action;
            Payload = payload;
        }

        public string EventType { get; }
        public string RuleType { get; }
        public int EventId { get; }
        public long? RecordId { get; }
        public string GatheredFrom { get; }
        public string GatheredLogName { get; }
        public IReadOnlyDictionary<string, string> Correlation { get; set; } = new Dictionary<string, string>();
        public string? WhenUtc { get; }
        public DateTime? WhenUtcDate { get; }
        public string? Who { get; }
        public string? ObjectAffected { get; }
        public string? Computer { get; }
        public string? Action { get; }
        public Dictionary<string, object?> Payload { get; }
    }

    private sealed class GroupAccumulator {
        public string CorrelationId { get; set; } = string.Empty;
        public IReadOnlyDictionary<string, string> Correlation { get; set; } = new Dictionary<string, string>();
        public int EventCount { get; set; }
        public DateTime? FirstSeenUtc { get; set; }
        public DateTime? LastSeenUtc { get; set; }
        public HashSet<string> EventType { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<int> EventIds { get; } = new();
        public HashSet<string> Machines { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class BucketAccumulator {
        public DateTime BucketStartUtc { get; set; }
        public int EventCount { get; set; }
        public HashSet<string> CorrelationIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
