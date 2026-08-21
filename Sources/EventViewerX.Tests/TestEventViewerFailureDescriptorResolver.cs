using EventViewerX.Reports;
using EventViewerX.Reports.Evtx;
using EventViewerX.Reports.Inventory;
using EventViewerX.Reports.Live;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventViewerFailureDescriptorResolver {
    [Fact]
    public void Resolve_EvtxIoError_IsRecoverableIoError() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(EvtxQueryFailureKind.IoError);

        Assert.Equal("io_error", descriptor.ErrorCode);
        Assert.Equal("io_error", descriptor.Category);
        Assert.True(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_EvtxException_MapsExplicitlyToQueryFailed() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(EvtxQueryFailureKind.Exception);

        Assert.Equal("query_failed", descriptor.ErrorCode);
        Assert.Equal("query_failed", descriptor.Category);
        Assert.True(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_LiveEventTimeout_IsRecoverableTimeout() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(LiveEventQueryFailureKind.Timeout);

        Assert.Equal("timeout", descriptor.ErrorCode);
        Assert.Equal("timeout", descriptor.Category);
        Assert.True(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_LiveEventHostUnavailable_IsRecoverableAndPreservesKind() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(LiveEventQueryFailureKind.HostUnavailable);

        Assert.Equal("host_unavailable", descriptor.ErrorCode);
        Assert.Equal("host_unavailable", descriptor.Category);
        Assert.True(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_LiveEventLogNotFound_IsNonRecoverableNotFound() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(LiveEventQueryFailureKind.LogNotFound);

        Assert.Equal("not_found", descriptor.ErrorCode);
        Assert.Equal("not_found", descriptor.Category);
        Assert.False(descriptor.Recoverable);
    }

    [Fact]
    public void Resolve_LiveEventException_MapsExplicitlyToQueryFailed() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(LiveEventQueryFailureKind.Exception);

        Assert.Equal("query_failed", descriptor.ErrorCode);
        Assert.Equal("query_failed", descriptor.Category);
        Assert.True(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_LiveStatsInvalidArgument_IsNonRecoverable() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(LiveStatsQueryFailureKind.InvalidArgument);

        Assert.Equal("invalid_argument", descriptor.ErrorCode);
        Assert.Equal("invalid_argument", descriptor.Category);
        Assert.False(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_LiveStatsInvalidQuery_IsNonRecoverableAndPreservesKind() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(LiveStatsQueryFailureKind.InvalidQuery);

        Assert.Equal("invalid_query", descriptor.ErrorCode);
        Assert.Equal("invalid_query", descriptor.Category);
        Assert.False(descriptor.Recoverable);
    }

    [Fact]
    public void Resolve_LiveStatsException_MapsExplicitlyToQueryFailed() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(LiveStatsQueryFailureKind.Exception);

        Assert.Equal("query_failed", descriptor.ErrorCode);
        Assert.Equal("query_failed", descriptor.Category);
        Assert.True(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_CatalogAccessDenied_IsNonRecoverable() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(EventCatalogFailureKind.AccessDenied);

        Assert.Equal("access_denied", descriptor.ErrorCode);
        Assert.Equal("access_denied", descriptor.Category);
        Assert.False(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_CatalogException_FallsBackToQueryFailed() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(EventCatalogFailureKind.Exception);

        Assert.Equal("query_failed", descriptor.ErrorCode);
        Assert.Equal("query_failed", descriptor.Category);
        Assert.True(descriptor.Recoverable);
        Assert.Equal("event_log_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_CatalogTimeout_IsRecoverableTimeout() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(EventCatalogFailureKind.Timeout);

        Assert.Equal("timeout", descriptor.ErrorCode);
        Assert.Equal("timeout", descriptor.Category);
        Assert.True(descriptor.Recoverable);
    }

    [Fact]
    public void Resolve_CatalogHostUnavailable_IsRecoverable() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(EventCatalogFailureKind.HostUnavailable);

        Assert.Equal("host_unavailable", descriptor.ErrorCode);
        Assert.Equal("host_unavailable", descriptor.Category);
        Assert.True(descriptor.Recoverable);
    }

    [Fact]
    public void Resolve_UsesProvidedEntityWhenSpecified() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(
            EvtxQueryFailureKind.InvalidArgument,
            entity: "custom_evtx_query");

        Assert.Equal("invalid_argument", descriptor.ErrorCode);
        Assert.Equal("custom_evtx_query", descriptor.Entity);
    }

    [Fact]
    public void Resolve_NormalizesBlankEntityToDefault() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve(
            LiveEventQueryFailureKind.Timeout,
            entity: "  ");

        Assert.Equal("timeout", descriptor.ErrorCode);
        Assert.Equal(EventViewerFailureDescriptorResolver.DefaultEntity, descriptor.Entity);
    }

    [Fact]
    public void Resolve_DefaultEntityDescriptorsReuseCachedInstances() {
        var first = EventViewerFailureDescriptorResolver.Resolve(EvtxQueryFailureKind.IoError);
        var second = EventViewerFailureDescriptorResolver.Resolve(EvtxQueryFailureKind.IoError);

        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_UnknownEvtxKind_FallsBackToQueryFailed() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve((EvtxQueryFailureKind)999);

        Assert.Equal("query_failed", descriptor.ErrorCode);
        Assert.Equal("query_failed", descriptor.Category);
        Assert.True(descriptor.Recoverable);
    }

    [Fact]
    public void Resolve_UnknownLiveEventKind_FallsBackToQueryFailed() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve((LiveEventQueryFailureKind)999);

        Assert.Equal("query_failed", descriptor.ErrorCode);
        Assert.Equal("query_failed", descriptor.Category);
        Assert.True(descriptor.Recoverable);
    }

    [Fact]
    public void Resolve_UnknownLiveStatsKind_FallsBackToQueryFailed() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve((LiveStatsQueryFailureKind)999);

        Assert.Equal("query_failed", descriptor.ErrorCode);
        Assert.Equal("query_failed", descriptor.Category);
        Assert.True(descriptor.Recoverable);
    }

    [Fact]
    public void Resolve_UnknownEventCatalogKind_FallsBackToQueryFailed() {
        var descriptor = EventViewerFailureDescriptorResolver.Resolve((EventCatalogFailureKind)999);

        Assert.Equal("query_failed", descriptor.ErrorCode);
        Assert.Equal("query_failed", descriptor.Category);
        Assert.True(descriptor.Recoverable);
    }

    [Fact]
    public void DescriptorCtor_BlankCategory_UsesUnknownFallback() {
        var descriptor = new EventViewerFailureDescriptor(
            errorCode: "",
            category: " ",
            entity: "",
            recoverable: true);

        Assert.Equal(EventViewerFailureDescriptor.DefaultErrorCode, descriptor.ErrorCode);
        Assert.Equal(EventViewerFailureDescriptor.UnknownCategory, descriptor.Category);
        Assert.Equal(EventViewerFailureDescriptorResolver.DefaultEntity, descriptor.Entity);
    }
}
