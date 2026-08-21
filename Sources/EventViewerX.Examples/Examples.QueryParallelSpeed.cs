using System.Diagnostics;

namespace EventViewerX.Examples {
    internal partial class Examples {

        public static void QueryParallelSpeed() {
            var machineNames = new List<string?> { "AD1", "AD2", "AD3" }; // Add your machine names here
            var eventIds = new List<int> { 4932, 4933 }; // Add your event IDs here

            Parallel.ForEach(machineNames, machine => {
                foreach (var eventObject in EventLogEngine.ReadChannel(
                             "Security",
                             new EventFilter { EventIds = eventIds },
                             machine)) {

                }
            });
        }

        public static async Task QueryParallelCompare() {
            var machineNames = new List<string?> { "AD1", "AD2", "AD3" }; // Add your machine names here
            var eventIds = new List<int> { 4932, 4933 }; // Add your event IDs here

            var stopwatch = Stopwatch.StartNew();
            int eventCount1 = 0;
            Parallel.ForEach(machineNames, machine => {
                foreach (var eventObject in EventLogEngine.ReadChannel(
                             "Security",
                             new EventFilter { EventIds = eventIds },
                             machine)) {
                    Interlocked.Increment(ref eventCount1);
                }
            });
            stopwatch.Stop();
            Console.WriteLine($"Parallel.ForEach method took {stopwatch.ElapsedMilliseconds} ms and returned {eventCount1} events.");

            stopwatch.Restart();
            int eventCount2 = 0;
            await foreach (var eventObject in EventLogEngine.ReadChannelsAsync(
                               ["Security"],
                               machineNames,
                               new EventFilter { EventIds = eventIds })) {
                eventCount2++;
            }
            stopwatch.Stop();
            Console.WriteLine($"QueryLogsParallel method took {stopwatch.ElapsedMilliseconds} ms and returned {eventCount2} events.");

            stopwatch.Restart();
            int eventCount3 = 0;
            foreach (var eventObject in EventLogEngine.ReadChannels(
                         ["Security"],
                         machineNames,
                         new EventFilter { EventIds = eventIds },
                         new EventLogQueryOptions { MaxConcurrency = 1 })) {
                eventCount3++;
            }
            stopwatch.Stop();
            Console.WriteLine($"QueryLogsSequential method took {stopwatch.ElapsedMilliseconds} ms and returned {eventCount3} events.");

            if (eventCount1 == eventCount2 && eventCount2 == eventCount3) {
                Console.WriteLine("All methods returned the same number of events.");
            } else {
                Console.WriteLine("The methods returned a different number of events.");
            }
        }
    }
}
