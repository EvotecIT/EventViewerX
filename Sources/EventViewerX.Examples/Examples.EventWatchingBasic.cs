namespace EventViewerX.Examples {
    internal partial class Examples {

        public static void EventWatchingBasic() {
            using var c1 = new WatchEvents {
                Warning = true,
                Verbose = true
            };
            c1.Watch("AD1", "Security", new List<int>() { 4627, 4624 }, e => Console.WriteLine($"Found event {e.Id}"));
        }
    }
}