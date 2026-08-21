using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventLogDetailsResult {
    [Fact]
    public void GetLogDetailsResult_KnownLog_ReturnsDetails() {
        if (!OperatingSystem.IsWindows()) return;

        EventLogDetailsResult result = EventLogCatalog.GetLogDetailsResult("Application");

        Assert.True(result.Success);
        Assert.Equal(EventLogDetailsStatus.Success, result.Status);
        Assert.NotNull(result.Details);
        Assert.Equal("Application", result.LogName);
    }

    [Fact]
    public void GetLogDetailsResult_MissingLog_ReturnsDiagnosticFailure() {
        if (!OperatingSystem.IsWindows()) return;

        EventLogDetailsResult result = EventLogCatalog.GetLogDetailsResult("Definitely-Missing-EventViewerX-UnitTest-Log");

        Assert.False(result.Success);
        Assert.Equal(EventLogDetailsStatus.LogConfigurationUnavailable, result.Status);
        Assert.Equal("Definitely-Missing-EventViewerX-UnitTest-Log", result.LogName);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorType));
    }

    [Fact]
    public void GetLogDetailsResult_ExistingSession_ReusesSession() {
        if (!OperatingSystem.IsWindows()) return;

        using var session = new EventLogSession();

        EventLogDetailsResult result = EventLogCatalog.GetLogDetailsResult(
            "Application",
            session,
            timeoutMs: 10000,
            machineName: Environment.MachineName,
            includeEventTimes: true);

        Assert.True(result.Success);
        Assert.Equal(EventLogDetailsStatus.Success, result.Status);
        Assert.NotNull(result.Details);
        Assert.Equal(Environment.MachineName, result.MachineName);
    }

    [Fact]
    public void CallerOwnedSessionOperationCompletesBeforeReturning() {
        bool completed = false;
        bool lateCleanupCalled = false;

        int result = EventLogCatalog.ExecuteSessionOperation(
            () => {
                Thread.Sleep(50);
                completed = true;
                return 42;
            },
            timeoutMilliseconds: 1,
            timeoutMessage: "Caller-owned work must not be abandoned.",
            CancellationToken.None,
            lateResultCleanup: _ =>
                lateCleanupCalled = true);

        Assert.Equal(42, result);
        Assert.True(completed);
        Assert.False(lateCleanupCalled);
    }

    [Fact]
    public void OpenSessionResult_LocalMachine_ReturnsSession() {
        if (!OperatingSystem.IsWindows()) return;

        using EventLogSessionOpenResult result = EventLogSessionManager.OpenSessionResult(null, purpose: "UnitTest", logName: "Application");

        Assert.True(result.Success);
        Assert.Equal(EventLogSessionOpenStatus.Success, result.Status);
        Assert.NotNull(result.Session);
        Assert.Equal("UnitTest", result.Purpose);
        Assert.Equal("Application", result.LogName);
    }

    [Fact]
    public void MapSessionFailureStatus_SessionConstructionFailureIsHostUnavailable() {
        Assert.Equal(
            EventLogDetailsStatus.HostUnavailable,
            EventLogCatalog.MapSessionFailureStatus(EventLogSessionOpenStatus.EventLogSessionUnavailable));
        Assert.Equal(
            EventLogDetailsStatus.Error,
            EventLogCatalog.MapSessionFailureStatus(EventLogSessionOpenStatus.Error));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetLogDetailsResult_RejectsNonPositiveTimeoutsAcrossOverloads(int timeoutMs) {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogCatalog.GetLogDetailsResult("Application", timeoutMs: timeoutMs));

        if (!OperatingSystem.IsWindows()) return;

        using var session = new EventLogSession();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventLogCatalog.GetLogDetailsResult("Application", session, timeoutMs));
    }

    [Fact]
    public void OpenSessionResult_DoesNotWriteBoundaryDiagnostics() {
        if (!OperatingSystem.IsWindows()) return;

        const string host =
            "eventviewerx-session-diagnostics.invalid";
        InternalLogger previous = Settings._logger;
        var logger = new InternalLogger();
        var warnings = new List<string>();
        var verboseMessages = new List<string>();
        logger.OnWarningMessage += (_, args) => warnings.Add(args.FullMessage);
        logger.OnVerboseMessage += (_, args) => verboseMessages.Add(args.FullMessage);
        Settings._logger = logger;
        EventLogSessionManager.ClearHostCache(host);
        try {
            EventLogSessionManager.MarkHostUnreachable(host);
            using EventLogSessionOpenResult result = EventLogSessionManager.OpenSessionResult(
                host,
                timeoutMs: 5000,
                purpose: "UnitTest",
                logName: "Application");

            Assert.False(result.Success);
            Assert.Equal(EventLogSessionOpenStatus.NegativeCache, result.Status);
            Assert.Empty(warnings);
            Assert.Empty(verboseMessages);
        } finally {
            EventLogSessionManager.ClearHostCache(host);
            Settings._logger = previous;
        }
    }

    [Fact]
    public void DiagnosticProperties_DistinguishPartialFailureFromCleanSuccess() {
        var result = new EventLogDetailsResult {
            LogName = "System",
            MachineName = "AD1",
            Status = EventLogDetailsStatus.EventTimesUnavailable,
            ErrorMessage = "Newest event could not be read."
        };

        Assert.True(result.HasDiagnosticFailure);
        Assert.Contains("System", result.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Contains("AD1", result.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Contains(nameof(EventLogDetailsStatus.EventTimesUnavailable), result.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Contains("Newest event could not be read.", result.DiagnosticMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyEventTimeFailure_ReportsTimeoutWithPartialDetailsStatus() {
        var result = new EventLogDetailsResult { Status = EventLogDetailsStatus.Success };

        EventLogCatalog.ApplyEventTimeFailure(result, new TimeoutException("event-time budget expired"));

        Assert.Equal(EventLogDetailsStatus.Timeout, result.Status);
        Assert.Equal(nameof(TimeoutException), result.ErrorType);
        Assert.Contains("event-time budget expired", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyEventTimeFailure_ReportsNonTimeoutReadFailure() {
        var result = new EventLogDetailsResult { Status = EventLogDetailsStatus.Success };

        EventLogCatalog.ApplyEventTimeFailure(result, new InvalidOperationException("event-time read failed"));

        Assert.Equal(EventLogDetailsStatus.EventTimesUnavailable, result.Status);
        Assert.Equal(nameof(InvalidOperationException), result.ErrorType);
        Assert.Contains("event-time read failed", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyEventTimeFailure_PreservesAccessDeniedClassification() {
        var result = new EventLogDetailsResult { Status = EventLogDetailsStatus.Success };

        EventLogCatalog.ApplyEventTimeFailure(result, new UnauthorizedAccessException("event-time access denied"));

        Assert.Equal(EventLogDetailsStatus.AccessDenied, result.Status);
        Assert.Equal(nameof(UnauthorizedAccessException), result.ErrorType);
    }

    [Fact]
    public void ApplyEventTimeFailure_PreservesEarlierPartialStatusAndAppendsDiagnostic() {
        var result = new EventLogDetailsResult {
            Status = EventLogDetailsStatus.Timeout,
            ErrorMessage = "Runtime information was unavailable.",
            ErrorType = nameof(TimeoutException)
        };

        EventLogCatalog.ApplyEventTimeFailure(result, new InvalidOperationException("event-time read failed"));

        Assert.Equal(EventLogDetailsStatus.Timeout, result.Status);
        Assert.Equal($"{nameof(TimeoutException)};{nameof(InvalidOperationException)}", result.ErrorType);
        Assert.Contains("Runtime information was unavailable.", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("event-time read failed", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MapLogInformationFailureStatus_PreservesActionableClassification() {
        Assert.Equal(
            EventLogDetailsStatus.Timeout,
            EventLogCatalog.MapLogInformationFailureStatus(new TimeoutException()));
        Assert.Equal(
            EventLogDetailsStatus.AccessDenied,
            EventLogCatalog.MapLogInformationFailureStatus(new UnauthorizedAccessException()));
        Assert.Equal(
            EventLogDetailsStatus.LogInformationUnavailable,
            EventLogCatalog.MapLogInformationFailureStatus(new InvalidOperationException()));
    }

    [Fact]
    public void WriteLogDetailsWarningIfNeeded_WarnsForPartialStatusOnly() {
        InternalLogger previous = Settings._logger;
        var logger = new InternalLogger();
        var warnings = new List<string>();
        logger.OnWarningMessage += (_, args) => warnings.Add(args.FullMessage);
        Settings._logger = logger;
        try {
            EventLogCatalog.WriteLogDetailsWarningIfNeeded(new EventLogDetailsResult {
                LogName = "System",
                Status = EventLogDetailsStatus.EventTimesUnavailable,
                ErrorMessage = "Timestamp read failed."
            });
            EventLogCatalog.WriteLogDetailsWarningIfNeeded(new EventLogDetailsResult {
                LogName = "Application",
                Status = EventLogDetailsStatus.Success
            });
        } finally {
            Settings._logger = previous;
        }

        string warning = Assert.Single(warnings);
        Assert.Contains("System", warning, StringComparison.Ordinal);
        Assert.Contains(nameof(EventLogDetailsStatus.EventTimesUnavailable), warning, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeDiagnosticStatus_PromotesGenericFailureButRetainsActionableFailure() {
        Assert.Equal(
            EventLogDetailsStatus.Timeout,
            EventLogCatalog.MergeDiagnosticStatus(
                EventLogDetailsStatus.LogInformationUnavailable,
                EventLogDetailsStatus.Timeout));
        Assert.Equal(
            EventLogDetailsStatus.AccessDenied,
            EventLogCatalog.MergeDiagnosticStatus(
                EventLogDetailsStatus.AccessDenied,
                EventLogDetailsStatus.Timeout));
        Assert.Equal(
            EventLogDetailsStatus.LogConfigurationUnavailable,
            EventLogCatalog.MergeDiagnosticStatus(
                EventLogDetailsStatus.LogConfigurationUnavailable,
                EventLogDetailsStatus.EventTimesUnavailable));
        Assert.Equal(
            EventLogDetailsStatus.AccessDenied,
            EventLogCatalog.MergeDiagnosticStatus(
                EventLogDetailsStatus.Error,
                EventLogDetailsStatus.AccessDenied));
        Assert.Equal(
            EventLogDetailsStatus.Timeout,
            EventLogCatalog.MergeDiagnosticStatus(
                EventLogDetailsStatus.Error,
                EventLogDetailsStatus.Timeout));
        Assert.Equal(
            EventLogDetailsStatus.Error,
            EventLogCatalog.MergeDiagnosticStatus(
                EventLogDetailsStatus.LogInformationUnavailable,
                EventLogDetailsStatus.Error));
    }

    [Fact]
    public void AppendResultDiagnostic_PreservesAllPartialFailureEvidence() {
        var result = new EventLogDetailsResult { Status = EventLogDetailsStatus.LogInformationUnavailable };

        EventLogCatalog.AppendResultDiagnostic(
            result,
            EventLogDetailsStatus.Timeout,
            "Runtime information timed out.",
            nameof(TimeoutException));
        EventLogCatalog.AppendResultDiagnostic(
            result,
            EventLogDetailsStatus.EventTimesUnavailable,
            "Event time failed.",
            nameof(InvalidOperationException));

        Assert.Equal(EventLogDetailsStatus.Timeout, result.Status);
        Assert.Contains("Runtime information timed out.", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Event time failed.", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal($"{nameof(TimeoutException)};{nameof(InvalidOperationException)}", result.ErrorType);
    }

    [Fact]
    public void DetailsOnlyCompatibilityApiOwnsWarningButTypedApiDoesNot() {
        if (!OperatingSystem.IsWindows()) return;

        InternalLogger previous = Settings._logger;
        var logger = new InternalLogger();
        var warnings = new List<string>();
        logger.OnWarningMessage += (_, args) => warnings.Add(args.FullMessage);
        Settings._logger = logger;
        try {
            EventLogDetailsResult typed = EventLogCatalog.GetLogDetailsResult("Definitely-Missing-EventViewerX-Warning-Test-Log");
            Assert.True(typed.HasDiagnosticFailure);
            Assert.Empty(warnings);

            EventLogDetails? details = EventLogCatalog.GetLogDetails("Definitely-Missing-EventViewerX-Warning-Test-Log");
            Assert.Null(details);
        } finally {
            Settings._logger = previous;
        }

        string warning = Assert.Single(warnings);
        Assert.Contains("Definitely-Missing-EventViewerX-Warning-Test-Log", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedSessionResultCanSuppressBoundaryDiagnostics() {
        if (!OperatingSystem.IsWindows()) return;

        InternalLogger previous = Settings._logger;
        var logger = new InternalLogger();
        var warnings = new List<string>();
        logger.OnWarningMessage += (_, args) => warnings.Add(args.FullMessage);
        Settings._logger = logger;
        try {
            using EventLogSessionOpenResult result = EventLogSessionManager.CreateSessionResult(
                null,
                "LogDetails",
                "Application",
                timeoutMs: 1000,
                localSessionFactory: static () => throw new InvalidOperationException("Expected session failure."),
                emitDiagnostics: false);

            Assert.False(result.Success);
            Assert.Equal(EventLogSessionOpenStatus.LocalSessionUnavailable, result.Status);
            Assert.Empty(warnings);
        } finally {
            Settings._logger = previous;
        }
    }

    [Fact]
    public void DisplayEventLogResults_PreservesExactNamesWhenSessionFails() {
        if (!OperatingSystem.IsWindows()) return;

        const string host =
            "eventviewerx-details-unavailable.invalid";
        EventLogSessionManager.ClearHostCache(host);
        try {
            EventLogSessionManager.MarkHostUnreachable(host);
            List<EventLogDetailsResult> results = EventLogCatalog.DisplayEventLogResults(
                    new[] { "Application", "System", "Application" },
                    host,
                    timeoutMs: 5000)
                .ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal(new[] { "Application", "System" }, results.Select(result => result.LogName));
            Assert.All(results, result => Assert.Equal(EventLogDetailsStatus.HostUnavailable, result.Status));
        } finally {
            EventLogSessionManager.ClearHostCache(host);
        }
    }

    [Fact]
    public void DisplayEventLogResultsSnapshotsLogNamesAtCallTime() {
        if (!OperatingSystem.IsWindows()) return;

        const string missingLog =
            "Definitely-Missing-EventViewerX-Snapshot-Log";
        string[] requestedLogs = { missingLog };
        IEnumerable<EventLogDetailsResult> stream =
            EventLogCatalog.DisplayEventLogResults(
                requestedLogs);

        requestedLogs[0] = "*";
        EventLogDetailsResult result =
            Assert.Single(
                stream);

        Assert.Equal(
            missingLog,
            result.LogName);
        Assert.False(
            result.Success);
    }

    [Fact]
    public void ParallelDisplayEventLogResultsSnapshotsTargetsAtCallTime() {
        if (!OperatingSystem.IsWindows()) return;

        var targets =
            new List<string?> {
                null
            };
        IEnumerable<EventLogDetailsResult> stream =
            EventLogCatalog.DisplayEventLogResultsParallel(
                new[] { "Application" },
                targets,
                maxDegreeOfParallelism: 1);

        targets[0] = "[";
        EventLogDetailsResult result =
            Assert.Single(
                stream);

        Assert.Equal(
            "Application",
            result.LogName);
        Assert.True(
            result.Success);
    }

    [Fact]
    public void DetailEnumeratorsHonorCancellationBeforeSessionSetup() {
        if (!OperatingSystem.IsWindows()) return;

        using var cancellation =
            new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            EventLogCatalog.DisplayEventLogResults(
                    new[] { "Application" },
                    cancellationToken:
                        cancellation.Token)
                .ToList());
        Assert.Throws<OperationCanceledException>(() =>
            EventLogCatalog.DisplayEventLogResultsParallel(
                    new[] { "Application" },
                    cancellationToken:
                        cancellation.Token)
                .ToList());
    }
}
