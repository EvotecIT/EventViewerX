namespace EventViewerX.Examples {
    internal partial class Examples {

        public static async Task QueryParallelWithCount() {
            var machineNames = new List<string?> { "AD1", "AD2", "AD3" }; // Add your machine names here
            var eventIds = new List<int> { 4932, 4933 }; // Add your event IDs here

            // Initialize a dictionary to keep track of the number of events per server
            var eventCounts = new Dictionary<string, int>();

            await foreach (var eventObject in EventLogEngine.ReadChannelsAsync(
                               ["Security"],
                               machineNames,
                               new EventFilter { EventIds = eventIds },
                               new EventLogQueryOptions { MaxConcurrency = 1 })) {
                // If the server is not yet in the dictionary, add it with a count of 1
                if (!eventCounts.ContainsKey(eventObject.ComputerName)) {
                    eventCounts[eventObject.ComputerName] = 1;
                }
                // If the server is already in the dictionary, increment its count
                else {
                    eventCounts[eventObject.ComputerName]++;
                }

                // Print an update every 2000 events
                if (eventCounts[eventObject.ComputerName] % 2000 == 0) {
                    Console.WriteLine("Server: {0}, Event Count: {1}", eventObject.ComputerName, eventCounts[eventObject.ComputerName]);
                }
            }

            // Print the final number of events per server
            foreach (var pair in eventCounts) {
                Console.WriteLine("Server: {0}, Final Event Count: {1}", pair.Key, pair.Value);
            }


            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }
    }
}
