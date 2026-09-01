using EventViewerX;

EventTypeCatalog.RegisterBuiltInRules();
EventTypeCatalog.Configure(EventRuleDiscoveryMode.ExplicitOnly);

EventTypeDefinition[] definitions = EventTypeCatalog.GetDefinitions().ToArray();
EventTypeDefinition[] leaves = definitions.Where(static definition => !definition.IsComposite).ToArray();
if (leaves.Length != 90 || leaves.Any(static definition =>
        definition.RecordType == null || definition.Sources.Count == 0 || definition.Fields.Count == 0)) {
    throw new InvalidOperationException(
        $"Explicit event projector catalog is incomplete: {leaves.Length} leaf definitions were available.");
}

EventTypeProjectionPlan plan = EventTypeCatalog.CompileProjectionPlan(
    new[] { EventType.ActiveDirectoryAuthentication });
if (plan.ExpandedTypes.Count == 0) {
    throw new InvalidOperationException("Explicit event projection plan is empty.");
}

Console.WriteLine($"EventViewerX NativeAOT explicit catalog: {leaves.Length} leaf definitions.");
