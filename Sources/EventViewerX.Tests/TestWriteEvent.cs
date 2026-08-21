using System;
using System.Diagnostics;
using Xunit;

namespace EventViewerX.Tests {
    public class TestWriteEvent {
        [Fact]
        public void InvalidCategoryThrows() {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ClassicEventLogManager.Write(
                    new ClassicEventWriteRequest {
                        SourceName = "TestSource",
                        LogName = "Application",
                        Message = "Test",
                        EntryType =
                            EventLogEntryType.Information,
                        Category = short.MaxValue + 1,
                        EventId = 1
                    })
            );
        }

        [Fact]
        public void MissingSourceIsNotCreatedByAnOrdinaryWrite() {
            if (!OperatingSystem.IsWindows()) return;
            string source =
                "EventViewerX-Missing-" +
                Guid.NewGuid().ToString("N");

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ClassicEventLogManager.Write(
                        new ClassicEventWriteRequest {
                            SourceName = source,
                            LogName = "Application",
                            Message = "No implicit registration",
                            EventId = 1
                        }));

            Assert.Contains(
                "not registered",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.False(
                ClassicEventLogManager.SourceExists(
                    source,
                    "Application"));
        }
    }
}
