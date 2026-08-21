namespace EventViewerX.Providers;

/// <summary>
/// Compiles a validated provider definition into a self-contained, code-free
/// Windows event metadata and message resource DLL without external tools.
/// </summary>
internal static class EventProviderManagedCompiler {
    internal const string Name = "EventViewerX.ManagedEventProviderCompiler";

    internal static string Version =>
        typeof(EventProviderManagedCompiler).Assembly
            .GetName()
            .Version?
            .ToString() ?? "0.0.0.0";

    internal static void Compile(
        EventProviderDefinition definition,
        string outputPath) {

        EventProviderDefinitionValidator.ValidateOrThrow(definition);
        EventProviderMessageCatalog catalog =
            EventProviderMessageCatalog.Create(definition);
        byte[] template = EventProviderCrimWriter.Write(
            definition,
            catalog);
        IReadOnlyDictionary<string, byte[]> messageTables = catalog.Cultures
            .ToDictionary(
                static culture => culture,
                culture => EventProviderMessageTableWriter.Write(
                    catalog.Messages(culture)),
                StringComparer.OrdinalIgnoreCase);
        byte[] resourceDll = EventProviderResourcePeWriter.Write(
            messageTables,
            definition.DefaultCulture,
            template);
        File.WriteAllBytes(outputPath, resourceDll);
    }
}
