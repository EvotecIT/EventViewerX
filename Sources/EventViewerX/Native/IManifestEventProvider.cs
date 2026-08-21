namespace EventViewerX.Native;

internal interface IManifestEventProvider {
    uint Write(
        ManifestEventDefinition definition,
        IReadOnlyList<object?> payload);
}
