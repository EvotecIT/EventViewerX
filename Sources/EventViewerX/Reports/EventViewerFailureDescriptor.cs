using System;
using EventViewerX.Reports.Evtx;
using EventViewerX.Reports.Inventory;
using EventViewerX.Reports.Live;

namespace EventViewerX.Reports;

/// <summary>
/// Engine-owned failure descriptor for stable machine-readable failure semantics.
/// </summary>
internal sealed class EventViewerFailureDescriptor {
    /// <summary>
    /// Fallback error code used when input is missing/blank.
    /// </summary>
    public const string DefaultErrorCode = "query_failed";

    /// <summary>
    /// Fallback category used when input is missing/blank.
    /// </summary>
    public const string UnknownCategory = "unknown";

    /// <summary>
    /// Initializes a new instance of the <see cref="EventViewerFailureDescriptor"/> class.
    /// </summary>
    public EventViewerFailureDescriptor(string errorCode, string category, string entity, bool recoverable) {
        ErrorCode = NormalizeToken(errorCode, DefaultErrorCode);
        Category = NormalizeToken(category, UnknownCategory);
        Entity = NormalizeEntityOrDefault(entity, EventViewerFailureDescriptorResolver.DefaultEntity);
        Recoverable = recoverable;
    }

    /// <summary>
    /// Stable error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Machine-readable category for recovery and planning.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Logical entity affected by the failure.
    /// </summary>
    public string Entity { get; }

    /// <summary>
    /// Whether the failure is expected to be recoverable in-session.
    /// </summary>
    public bool Recoverable { get; }

    private static string NormalizeToken(string? value, string fallback) {
        var trimmed = value?.Trim();
        if (trimmed is null || trimmed.Length == 0) {
            return fallback;
        }

        return trimmed;
    }

    internal static string NormalizeEntityOrDefault(string? entity, string fallbackEntity) {
        var trimmed = entity?.Trim();
        if (trimmed is null || trimmed.Length == 0) {
            return fallbackEntity;
        }

        return trimmed;
    }
}

/// <summary>
/// Resolves EventViewerX failure kinds to stable engine-facing failure descriptors.
/// </summary>
/// <remarks>
/// Recoverability policy:
/// <list type="bullet">
/// <item><description><c>invalid_argument</c>, <c>invalid_query</c>, <c>access_denied</c>, <c>not_found</c> => non-recoverable.</description></item>
/// <item><description><c>timeout</c>, <c>host_unavailable</c>, <c>io_error</c>, <c>query_failed</c> => recoverable.</description></item>
/// </list>
/// Unknown enum values are mapped to <c>query_failed</c>.
/// Consumer guidance: retry or adjust scope for recoverable descriptors; for non-recoverable descriptors, prompt for corrected inputs/permissions before retrying.
/// </remarks>
internal static class EventViewerFailureDescriptorResolver {
    /// <summary>
    /// Default failure entity used when no explicit entity is provided.
    /// </summary>
    public const string DefaultEntity = "event_log_query";

    private static readonly EventViewerFailureDescriptor InvalidArgumentDefault = new("invalid_argument", "invalid_argument", DefaultEntity, recoverable: false);
    private static readonly EventViewerFailureDescriptor InvalidQueryDefault = new("invalid_query", "invalid_query", DefaultEntity, recoverable: false);
    private static readonly EventViewerFailureDescriptor AccessDeniedDefault = new("access_denied", "access_denied", DefaultEntity, recoverable: false);
    private static readonly EventViewerFailureDescriptor NotFoundDefault = new("not_found", "not_found", DefaultEntity, recoverable: false);
    private static readonly EventViewerFailureDescriptor TimeoutDefault = new("timeout", "timeout", DefaultEntity, recoverable: true);
    private static readonly EventViewerFailureDescriptor HostUnavailableDefault = new("host_unavailable", "host_unavailable", DefaultEntity, recoverable: true);
    private static readonly EventViewerFailureDescriptor IoErrorDefault = new("io_error", "io_error", DefaultEntity, recoverable: true);
    private static readonly EventViewerFailureDescriptor QueryFailedDefault = new("query_failed", "query_failed", DefaultEntity, recoverable: true);

    /// <summary>
    /// Resolves EVTX query failure kind to a typed failure descriptor.
    /// </summary>
    /// <example>
    /// <code>
    /// var descriptor = EventViewerFailureDescriptorResolver.Resolve(EvtxQueryFailureKind.IoError);
    /// var custom = EventViewerFailureDescriptorResolver.Resolve(EvtxQueryFailureKind.InvalidArgument, "security_evtx_query");
    /// </code>
    /// </example>
    /// <remarks><c>Exception</c> and unknown values map to <c>query_failed</c>.</remarks>
    public static EventViewerFailureDescriptor Resolve(EvtxQueryFailureKind kind, string entity = DefaultEntity)
        => kind switch {
            EvtxQueryFailureKind.InvalidArgument => InvalidArgument(entity),
            EvtxQueryFailureKind.AccessDenied => AccessDenied(entity),
            EvtxQueryFailureKind.NotFound => NotFound(entity),
            EvtxQueryFailureKind.IoError => IoError(entity),
            EvtxQueryFailureKind.Exception => QueryFailed(entity),
            _ => QueryFailed(entity)
        };

    /// <summary>
    /// Resolves live-event query failure kind to a typed failure descriptor.
    /// </summary>
    /// <remarks><c>Exception</c> and unknown values map to <c>query_failed</c>.</remarks>
    public static EventViewerFailureDescriptor Resolve(LiveEventQueryFailureKind kind, string entity = DefaultEntity)
        => kind switch {
            LiveEventQueryFailureKind.InvalidArgument => InvalidArgument(entity),
            LiveEventQueryFailureKind.InvalidQuery => InvalidQuery(entity),
            LiveEventQueryFailureKind.LogNotFound => NotFound(entity),
            LiveEventQueryFailureKind.AccessDenied => AccessDenied(entity),
            LiveEventQueryFailureKind.Timeout => Timeout(entity),
            LiveEventQueryFailureKind.HostUnavailable => HostUnavailable(entity),
            LiveEventQueryFailureKind.Exception => QueryFailed(entity),
            _ => QueryFailed(entity)
        };

    /// <summary>
    /// Resolves live-stats query failure kind to a typed failure descriptor.
    /// </summary>
    /// <remarks><c>Exception</c> and unknown values map to <c>query_failed</c>.</remarks>
    public static EventViewerFailureDescriptor Resolve(LiveStatsQueryFailureKind kind, string entity = DefaultEntity)
        => kind switch {
            LiveStatsQueryFailureKind.InvalidArgument => InvalidArgument(entity),
            LiveStatsQueryFailureKind.InvalidQuery => InvalidQuery(entity),
            LiveStatsQueryFailureKind.LogNotFound => NotFound(entity),
            LiveStatsQueryFailureKind.AccessDenied => AccessDenied(entity),
            LiveStatsQueryFailureKind.Timeout => Timeout(entity),
            LiveStatsQueryFailureKind.HostUnavailable => HostUnavailable(entity),
            LiveStatsQueryFailureKind.Exception => QueryFailed(entity),
            _ => QueryFailed(entity)
        };

    /// <summary>
    /// Resolves event-catalog query failure kind to a typed failure descriptor.
    /// </summary>
    /// <remarks><c>Exception</c> and unknown values map to <c>query_failed</c>.</remarks>
    public static EventViewerFailureDescriptor Resolve(EventCatalogFailureKind kind, string entity = DefaultEntity)
        => kind switch {
            EventCatalogFailureKind.InvalidArgument => InvalidArgument(entity),
            EventCatalogFailureKind.AccessDenied => AccessDenied(entity),
            EventCatalogFailureKind.Timeout => Timeout(entity),
            EventCatalogFailureKind.HostUnavailable => HostUnavailable(entity),
            EventCatalogFailureKind.Exception => QueryFailed(entity),
            _ => QueryFailed(entity)
        };

    private static EventViewerFailureDescriptor InvalidArgument(string entity) => Create(InvalidArgumentDefault, entity);
    private static EventViewerFailureDescriptor InvalidQuery(string entity) => Create(InvalidQueryDefault, entity);
    private static EventViewerFailureDescriptor AccessDenied(string entity) => Create(AccessDeniedDefault, entity);
    private static EventViewerFailureDescriptor NotFound(string entity) => Create(NotFoundDefault, entity);
    private static EventViewerFailureDescriptor Timeout(string entity) => Create(TimeoutDefault, entity);
    private static EventViewerFailureDescriptor HostUnavailable(string entity) => Create(HostUnavailableDefault, entity);
    private static EventViewerFailureDescriptor IoError(string entity) => Create(IoErrorDefault, entity);
    private static EventViewerFailureDescriptor QueryFailed(string entity) => Create(QueryFailedDefault, entity);

    private static EventViewerFailureDescriptor Create(EventViewerFailureDescriptor template, string entity) {
        var normalizedEntity = EventViewerFailureDescriptor.NormalizeEntityOrDefault(entity, DefaultEntity);
        if (string.Equals(normalizedEntity, DefaultEntity, StringComparison.Ordinal)) {
            return template;
        }

        return new EventViewerFailureDescriptor(
            template.ErrorCode,
            template.Category,
            normalizedEntity,
            template.Recoverable);
    }
}
