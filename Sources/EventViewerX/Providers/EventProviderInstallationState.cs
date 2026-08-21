using System.Text.Json;

namespace EventViewerX.Providers;

internal sealed class EventProviderInstallationState {
    public string ProviderName { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public string ActiveVersion { get; set; } = string.Empty;
    public string ActiveDirectoryName { get; set; } = string.Empty;
    public string PackageSha256 { get; set; } = string.Empty;
    public DateTimeOffset InstalledAtUtc { get; set; }
    public bool IsSigned { get; set; }
    public string SignerThumbprint { get; set; } = string.Empty;
}

internal static class EventProviderInstallationStore {
    internal const string StateFileName = "installation.json";
    internal const string ArchivedPackageFileName = "provider.evxprovider";

    internal static EventProviderInstallationState? Load(
        string providerRoot) {

        string path = Path.Combine(providerRoot, StateFileName);
        if (!File.Exists(path)) {
            return null;
        }
        return JsonSerializer.Deserialize<EventProviderInstallationState>(
                   File.ReadAllText(path, Encoding.UTF8),
                   EventProviderDefinitionJson.SerializerOptions) ??
               throw new InvalidDataException(
                   $"Provider installation state '{path}' is invalid.");
    }

    internal static void Save(
        string providerRoot,
        EventProviderInstallationState state,
        Action<string>? deleteTemporary = null) {

        Directory.CreateDirectory(providerRoot);
        string path = Path.Combine(providerRoot, StateFileName);
        string temporary = path + "." +
                           Guid.NewGuid().ToString("N") +
                           ".tmp";
        try {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    state,
                    EventProviderDefinitionJson.SerializerOptions),
                new UTF8Encoding(false));
            if (File.Exists(path)) {
                File.Replace(temporary, path, null);
            } else {
                File.Move(temporary, path);
            }
        } finally {
            try {
                if (File.Exists(temporary)) {
                    (deleteTemporary ?? File.Delete)(
                        temporary);
                }
            } catch {
            }
        }
    }
}
