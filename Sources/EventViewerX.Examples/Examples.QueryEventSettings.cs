namespace EventViewerX.Examples {
    internal partial class Examples {

        public static void QueryBasicEventLogList() {

            Settings.Logger.IsVerbose = true;
            Settings.Logger.IsWarning = true;
            Settings.Logger.IsError = true;

            foreach (var test in EventLogCatalog.DisplayEventLogs()) {
                Console.WriteLine(test);
            }
        }

        public static void ShowChannelPolicy(string logName = "Application", string? machineName = null) {
            var pol = EventLogChannelPolicyService.Get(
                logName,
                machineName);
            if (pol == null) {
                Console.WriteLine($"No policy for '{logName}'");
                return;
            }
            Console.WriteLine($"{pol.LogName} @ {pol.MachineName ?? Environment.MachineName} | Enabled={pol.IsEnabled} Size={pol.MaximumSizeInBytes} Mode={pol.Mode} Path={pol.LogFilePath}");
        }

        public static void SetChannelPolicyExample(string logName = "Application", string? machineName = null) {
            // Example: increase size for Application log (classic) to 64 MB and set circular
            var ok = EventLogChannelPolicyService.Apply(new ChannelPolicy {
                LogName = logName,
                MachineName = machineName,
                MaximumSizeInBytes = 64L * 1024 * 1024,
                Mode = System.Diagnostics.Eventing.Reader.EventLogMode.Circular
            });
            Console.WriteLine($"Set policy result for '{logName}': {ok}");
        }
    }
}
