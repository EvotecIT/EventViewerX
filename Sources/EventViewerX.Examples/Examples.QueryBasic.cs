namespace EventViewerX.Examples {
    internal partial class Examples {

        public static void QueryBasic() {
            EventLogEngine.ReadChannel(
                "Application",
                new EventFilter { EventIds = [1008, 4098, 1001] },
                "AD1");
            EventLogEngine.ReadChannel(
                "Application",
                new EventFilter { EventIds = [1001] },
                "AD1");
            EventLogEngine.ReadChannel(
                "Security",
                new EventFilter { EventIds = [4627, 4624] },
                "AD1");
        }

        public static void QueryBasicForwardedEvents() {
            var list = EventLogEngine.ReadChannel(
                "ForwardedEvents",
                new EventFilter { EventIds = [4722, 4738] });
            foreach (var test in list) {
                Console.WriteLine(test.Id + " " + test.QueriedMachine + " " + test.MachineName + " " + test.ContainerLog + " " + test.LogName);
            }
            Console.WriteLine("Count: " + list.Count());
        }

        public static void QueryBasicWithOutput() {
            foreach (var eventObject in EventLogEngine.ReadChannel(
                         "Security",
                         new EventFilter { EventIds = [4932, 4933] },
                         "AD1")) {
                Console.WriteLine("Event ID: {0}", eventObject.Id);
                Console.WriteLine("Data count: " + eventObject.Data.Count);
                foreach (var data in eventObject.Data) {
                    Console.WriteLine("[-] Data: {0} - {1}", data.Key, data.Value);
                }
            }
        }

        public static async Task QueryBasicParallel() {
            await foreach (var eventObject in EventLogEngine.ReadChannelsAsync(
                               ["Security"],
                               ["AD1"],
                               new EventFilter { EventIds = [4932, 4933] })) {
                Console.WriteLine("Event ID: {0}", eventObject.Id);
                Console.WriteLine("Data count: " + eventObject.Data.Count);
                foreach (var data in eventObject.Data) {
                    Console.WriteLine("[-] Data: {0} - {1}", data.Key, data.Value);
                }
            }
        }
    }
}
