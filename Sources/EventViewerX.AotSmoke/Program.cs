using EventViewerX;

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

string entraConnectQuery = EventDefinitionCompiler.BuildQueryXml(
    new[] { EventType.EntraConnectHealth });
if (!entraConnectQuery.Contains("Provider[@Name='Directory Synchronization']", StringComparison.Ordinal) ||
    !entraConnectQuery.Contains("Provider[@Name='ADSync']", StringComparison.Ordinal)) {
    throw new InvalidOperationException("Explicit provider metadata was not retained in the typed query.");
}

Console.WriteLine($"EventViewerX NativeAOT default catalog: {leaves.Length} leaf definitions.");

LegacyJsonSmoke.Run();
await CrashSmoke.RunAsync();
