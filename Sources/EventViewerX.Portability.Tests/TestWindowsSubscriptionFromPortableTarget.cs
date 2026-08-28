using System.Security.Principal;
using Xunit;

namespace EventViewerX.Portability.Tests;

public sealed class TestWindowsSubscriptionFromPortableTarget {
    [Fact]
    public void FutureSubscriptionDeliversWhenHostUsesPortableTarget() {
        if (!OperatingSystem.IsWindows() || !IsAdministrator()) {
            return;
        }
        string suffix = Guid.NewGuid().ToString("N");
        string logName = $"EVX{suffix}Portable";
        string sourceName = $"EVXS{suffix}PortableSource";
        try {
            ClassicEventLogManager.EnsureLog(new ClassicEventLogConfiguration {
                LogName = logName,
                SourceName = sourceName,
                MaximumKilobytes = 256,
                OverflowAction = System.Diagnostics.OverflowAction.OverwriteAsNeeded
            });
            using var delivered = new ManualResetEventSlim();
            EventObject? received = null;
            EventLogSubscriptionFailure? failure = null;
            using var subscription = new EventLogSubscription(
                new EventLogSubscriptionQuery(logName) {
                    Start = EventLogSubscriptionStart.Future,
                    XPath = "*[System[EventID=7001]]",
                    ReadMode = EventReadMode.StructuredData,
                    BufferCapacity = 1
                },
                eventObject => {
                    received = eventObject;
                    delivered.Set();
                },
                subscriptionFailure => {
                    failure = subscriptionFailure;
                    delivered.Set();
                });

            ClassicEventLogManager.Write(new ClassicEventWriteRequest {
                LogName = logName,
                SourceName = sourceName,
                EventId = 7001,
                Message = "portable-target-subscription"
            });

            Assert.True(
                delivered.Wait(TimeSpan.FromSeconds(10)),
                failure?.Exception.ToString());
            Assert.Null(failure);
            Assert.NotNull(received);
            Assert.Equal(7001, received!.Id);
        } finally {
            if (ClassicEventLogManager.LogExists(logName)) {
                ClassicEventLogManager.RemoveLog(logName);
            }
        }
    }

    private static bool IsAdministrator() {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
