using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EventViewerX.Examples {
    internal partial class Examples {
        public static void WatchBasic() {
            var watcher = WatcherManager.StartWatcher(
                "basic",
                Environment.MachineName,
                "Security",
                new List<int> { 4624, 4625 },
                new List<EventType>(),
                e => Console.WriteLine($"Event {e.Id} arrived"),
                false,
                false,
                0,
                null);
            Thread.Sleep(TimeSpan.FromSeconds(30));
            Console.WriteLine($"Events found: {watcher.EventsFound}");
            WatcherManager.StopAll();
        }

        public static void WatchEventTypes() {
            var watcher = WatcherManager.StartWatcher(
                "named",
                Environment.MachineName,
                "System",
                EventTypeCatalog.GetSources(new[] { EventType.OSCrash })
                    .Single(source => source.LogName == "System")
                    .EventIds.ToList(),
                new List<EventType> { EventType.OSCrash },
                e => Console.WriteLine($"Named event {e.Id}"),
                false,
                false,
                0,
                null);
            Thread.Sleep(TimeSpan.FromSeconds(30));
            WatcherManager.StopWatcher(watcher.Id);
        }

        public static void WatchWithStopAfter() {
            var watcher = WatcherManager.StartWatcher(
                "stopAfter",
                Environment.MachineName,
                "Security",
                new List<int> { 4625 },
                new List<EventType>(),
                e => Console.WriteLine("Event " + e.Id),
                false,
                false,
                2,
                null);
            while (WatcherManager.GetWatchers("stopAfter").Any()) {
                Thread.Sleep(1000);
            }
        }

        public static void WatchWithTimeout() {
            var watcher = WatcherManager.StartWatcher(
                "timeout",
                Environment.MachineName,
                "Application",
                new List<int> { 1000 },
                new List<EventType>(),
                e => Console.WriteLine("Event " + e.Id),
                false,
                false,
                0,
                TimeSpan.FromSeconds(10));
            Thread.Sleep(TimeSpan.FromSeconds(15));
            Console.WriteLine($"Ended at {watcher.EndTime}");
        }

        public static void WatchDuplicateName() {
            var first = WatcherManager.StartWatcher(
                "duplicate",
                Environment.MachineName,
                "Application",
                new List<int> { 1 },
                new List<EventType>(),
                _ => { },
                false,
                false,
                0,
                null);
            var second = WatcherManager.StartWatcher(
                "duplicate",
                Environment.MachineName,
                "Application",
                new List<int> { 1 },
                new List<EventType>(),
                _ => { },
                false,
                false,
                0,
                null);
            Console.WriteLine(first == second ? "Same watcher" : "Different watcher");
            WatcherManager.StopAll();
        }
    }
}
