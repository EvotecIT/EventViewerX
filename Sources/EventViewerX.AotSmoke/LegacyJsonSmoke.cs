using EventViewerX;

internal static class LegacyJsonSmoke {
    internal static void Run() {
        string fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        EventDetectionPlan plan = EventDetectionPlan.FromJson(File.ReadAllText(Path.Combine(fixtures, "plan-4.0.2.json")));
        EventDetectionPack pack = EventDetectionPack.ParseJson(File.ReadAllText(Path.Combine(fixtures, "pack-4.0.2.json")));
        if (plan.PlanHash != "3E0B83985F4C28F1CD6320242A7EAD2AE91095FFA2026D90AF48358B505203F7" ||
            !plan.RequiredEventTypes.SequenceEqual(new[] { EventType.OSBugCheck }) ||
            pack.ContentHash != "5B1B70F973944D186EE8BF5AE62F0668EC044730962818CC4FAC86601C9FB125" ||
            !pack.Validate().IsValid) {
            throw new InvalidOperationException("The native reader changed legacy selector or integrity contracts.");
        }
    }
}
