using System.Collections.Generic;
using Xunit;

namespace EventViewerX.Tests {
    public class TestNetworkMonitorEvents {
        [Fact]
        public void EventInfoContainsNetworkMonitorEvents() {
            var info = EventTypeCatalog.GetSourceMap(new List<EventType> {
                EventType.NetworkMonitorDriverLoaded,
                EventType.NetworkPromiscuousMode
            });

            Assert.Contains(6, info["System"]);
            Assert.Contains(7035, info["System"]);
            Assert.Contains(7045, info["System"]);
            Assert.Contains(10400, info["System"]);
            Assert.Contains(10401, info["System"]);
        }
    }
}
