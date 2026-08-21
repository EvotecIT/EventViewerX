using EventViewerX.Rules.Windows;

namespace EventViewerX.Examples {
    internal partial class Examples {
        public static async Task FindNetworkMonitorEvents() {
            var query = new EventTypeQuery([
                EventType.NetworkMonitorDriverLoaded,
                EventType.NetworkPromiscuousMode
            ]);
            await foreach (var evt in EventTypeEngine.ReadAsync(query)) {
                var computer = evt switch {
                    NetworkMonitorDriverLoaded driver => driver.Computer,
                    NetworkPromiscuousMode promiscuous => promiscuous.Computer,
                    _ => evt.MachineName
                };

                Console.WriteLine($"Event: {evt.TypeName} {evt.EventId} on {computer}");
            }
        }
    }
}
