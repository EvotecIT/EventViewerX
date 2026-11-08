using System.Collections.Generic;
using Xunit;

namespace EventViewerX.Tests {
    public class TestNetworkMonitorEvents {
        [Fact]
        public void EventInfoContainsNetworkMonitorEvents() {
            var info = EventTypeCatalog.GetSourceMap(new List<EventType> {
                EventType.NetworkMonitorDriverLoaded
            });

            Assert.DoesNotContain(6, info["System"]);
            Assert.Contains(7035, info["System"]);
            Assert.Contains(7045, info["System"]);
            Assert.DoesNotContain(10400, info["System"]);
            Assert.DoesNotContain(10401, info["System"]);
            Assert.Throws<NotSupportedException>(() => EventTypeCatalog.GetSourceMap(
                new[] { EventType.NetworkPromiscuousMode }));
        }
    }
}
