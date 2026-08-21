namespace EventViewerX.Providers;

internal static class EventProviderPackageLayout {
    internal const string DefinitionFileName = "provider.definition.json";
    internal const string ManifestFileName = "provider.man";
    internal const string PackageManifestFileName = "package.json";
    internal const string ResourceFileName = "provider.resources.dll";
    internal const string SchemaLockFileName = "schema-lock.json";

    internal static readonly DateTimeOffset StableZipTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
