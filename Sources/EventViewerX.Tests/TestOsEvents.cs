using System.Collections.Generic;
using Xunit;

namespace EventViewerX.Tests {
    public class TestOsEvents {
        [Fact]
        public void EventInfoContainsOsEvents() {
            var info = EventTypeCatalog.GetSourceMap(new List<EventType> {
                EventType.OSStartup,
                EventType.OSShutdown,
                EventType.OSUncleanShutdown,
                EventType.OSStartupSecurity,
                EventType.OSCrashOnAuditFailRecovery
            });

            Assert.Contains(12, info["System"]);
            Assert.Contains(13, info["System"]);
            Assert.Contains(41, info["System"]);
            Assert.Contains(4608, info["Security"]);
            Assert.Contains(4621, info["Security"]);
        }
    }
}
