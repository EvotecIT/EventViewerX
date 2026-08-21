namespace EventViewerX.Examples {
    internal partial class Examples {
        public static void ShowCollectorSubscriptions(string? machineName = null) {
            foreach (var sub in CollectorSubscriptionManager.GetCollectorSubscriptions(machineName)) {
                Console.WriteLine($"{sub.Name} Enabled={sub.Enabled} ContentFormat={sub.ContentFormat} Delivery={sub.DeliveryMode} Queries={sub.Queries.Count}");
            }
        }

        public static void EnableSubscriptionExample(string name, bool enabled, string? machineName = null) {
            CollectorSubscriptionUpdateResult result =
                CollectorSubscriptionManager
                    .SetCollectorSubscriptionEnabled(
                        name,
                        enabled,
                        machineName);
            Console.WriteLine(
                $"Enable '{name}' -> {enabled}: Success={result.Success}, Changed={result.Changed}");
        }
    }
}
