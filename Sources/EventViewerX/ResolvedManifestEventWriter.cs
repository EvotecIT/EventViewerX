using EventViewerX.Native;
using EventViewerX.Providers;
using ProviderEventDefinition =
    EventViewerX.Providers.EventProviderEventDefinition;

namespace EventViewerX;

/// <summary>
/// Caches one registered provider/event schema and accepts named dictionaries
/// or typed payload objects for efficient repeated writes.
/// </summary>
public sealed class ResolvedManifestEventWriter : IDisposable {
    private readonly RegisteredManifestEventProvider _provider;
    private bool _disposed;

    private ResolvedManifestEventWriter(
        ManifestEventDefinition definition) {

        Definition = definition;
        _provider = new RegisteredManifestEventProvider(
            definition.ProviderId);
    }

    /// <summary>Resolved native event schema.</summary>
    public ManifestEventDefinition Definition { get; }

    /// <summary>
    /// Resolves an event by provider, identifier, and optional schema version.
    /// This works for any registered manifest provider.
    /// </summary>
    public static ResolvedManifestEventWriter Open(
        string providerName,
        int id,
        byte? version = null) {

        ManifestEventDefinition definition =
            ManifestEventWriter.ResolveDefinition(
                new ManifestEventWriteRequest {
                    ProviderName = providerName,
                    Id = id,
                    Version = version
                });
        return new ResolvedManifestEventWriter(definition);
    }

    /// <summary>
    /// Resolves a friendly event name from an EventViewerX-managed provider
    /// package.
    /// </summary>
    public static ResolvedManifestEventWriter Open(
        string providerName,
        string eventName,
        byte? version = null,
        string packageRootPath = "") {

        if (string.IsNullOrWhiteSpace(eventName)) {
            throw new ArgumentException(
                "Event name cannot be empty.",
                nameof(eventName));
        }
        EventProviderDefinition providerDefinition =
            EventProviderPackageManager.GetDefinition(
                providerName,
                packageRootPath);
        ProviderEventDefinition[] matches =
            providerDefinition.Events
                .Where(item => string.Equals(
                    item.Name,
                    eventName,
                    StringComparison.OrdinalIgnoreCase) &&
                    (!version.HasValue ||
                     item.Version == version.Value))
                .ToArray();
        if (matches.Length == 0) {
            throw new ArgumentException(
                $"Managed provider '{providerName}' does not declare event '{eventName}'" +
                (version.HasValue
                    ? $" version {version.Value}."
                    : "."),
                nameof(eventName));
        }
        if (matches.Length > 1) {
            throw new ArgumentException(
                $"Managed provider '{providerName}' declares event '{eventName}' in multiple versions; specify Version.",
                nameof(version));
        }
        return Open(
            providerName,
            matches[0].Id,
            matches[0].Version);
    }

    /// <summary>Writes values by manifest field name, independent of dictionary order.</summary>
    public ManifestEventWriteResult Write(
        IReadOnlyDictionary<string, object?> values) {

        if (_disposed) {
            throw new ObjectDisposedException(
                nameof(ResolvedManifestEventWriter));
        }
        if (values == null) {
            throw new ArgumentNullException(nameof(values));
        }
        return ManifestEventWriter.Write(
            Definition,
            ManifestEventWriter.OrderNamedPayload(
                Definition,
                values),
            _provider);
    }

    /// <summary>
    /// Writes a typed payload whose public properties map to manifest field
    /// names. Use <see cref="EventProviderPayloadFieldAttribute"/> to override
    /// names and stable ordering.
    /// </summary>
    public ManifestEventWriteResult Write<TPayload>(
        TPayload payload) {

        return Write(
            EventProviderTypedPayload.Read(payload));
    }

    /// <summary>Releases the cached native provider registration.</summary>
    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        _provider.Dispose();
    }
}
