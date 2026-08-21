using System.Diagnostics.Eventing.Reader;
using System.Globalization;

namespace EventViewerX.Providers;

internal static class EventProviderManifestRegistrar {
    internal static void Install(
        string manifestPath,
        string resourcePath,
        TimeSpan timeout) {

        EventProviderProcessResult result =
            EventProviderProcessRunner.Run(
                ToolPath("wevtutil.exe"),
                new[] {
                    "im",
                    manifestPath,
                    "/rf:" + resourcePath,
                    "/mf:" + resourcePath
                },
                Path.GetDirectoryName(manifestPath)!,
                timeout);
        EventProviderProcessRunner.EnsureSuccess(
            result,
            "Windows Event Log manifest installer");
    }

    internal static void Uninstall(
        string manifestPath,
        TimeSpan timeout) {

        EventProviderProcessResult result =
            EventProviderProcessRunner.Run(
                ToolPath("wevtutil.exe"),
                new[] {
                    "um",
                    manifestPath
                },
                Path.GetDirectoryName(manifestPath)!,
                timeout);
        EventProviderProcessRunner.EnsureSuccess(
            result,
            "Windows Event Log manifest uninstaller");
    }

    internal static void EnsureReadable(
        string directory,
        TimeSpan timeout) {

        EventProviderManagedDirectorySecurity
            .EnsureExact(
                directory,
                timeout);
    }

    internal static void Verify(
        EventProviderDefinition definition) {

        Exception? lastError = null;
        for (int attempt = 0; attempt < 10; attempt++) {
            try {
                using var metadata = new ProviderMetadata(
                    definition.Name,
                    EventLogSession.GlobalSession,
                    CultureInfo.GetCultureInfo(
                        definition.DefaultCulture));
                if (metadata.Id != definition.Id) {
                    throw new InvalidDataException(
                        $"Registered provider '{definition.Name}' has GUID " +
                        $"{metadata.Id}, expected {definition.Id}.");
                }
                var events = new HashSet<string>(
                    metadata.Events.Select(item =>
                        item.Id + ":" + item.Version),
                    StringComparer.Ordinal);
                string[] missingEvents = definition.Events
                    .Where(item => !events.Contains(
                        item.Id + ":" + item.Version))
                    .Select(item => item.Id + ":" + item.Version)
                    .ToArray();
                if (missingEvents.Length > 0) {
                    throw new InvalidDataException(
                        $"Registered provider '{definition.Name}' is missing event schema(s): " +
                        string.Join(", ", missingEvents));
                }
                var channels = new HashSet<string>(
                    metadata.LogLinks.Select(item =>
                        item.LogName ?? string.Empty),
                    StringComparer.OrdinalIgnoreCase);
                string[] missingChannels = definition.Channels
                    .Where(item => !channels.Contains(item.Name))
                    .Select(item => item.Name)
                    .ToArray();
                if (missingChannels.Length > 0) {
                    throw new InvalidDataException(
                        $"Registered provider '{definition.Name}' is missing channel(s): " +
                        string.Join(", ", missingChannels));
                }
                return;
            } catch (Exception exception)
                when (exception is EventLogException ||
                      exception is InvalidDataException) {
                lastError = exception;
                Thread.Sleep(100);
            }
        }
        throw new InvalidOperationException(
            $"Windows did not expose the installed provider '{definition.Name}' with its expected schema.",
            lastError);
    }

    internal static bool IsRegistered(string providerName) {
        try {
            using var metadata = new ProviderMetadata(
                providerName,
                EventLogSession.GlobalSession,
                CultureInfo.InvariantCulture);
            _ = metadata.Id;
            return true;
        } catch (EventLogException exception)
            when (IsMissingRegistrationFailure(exception)) {
            return false;
        }
    }

    internal static bool IsMissingRegistrationFailure(
        EventLogException exception) {

        return exception is EventLogNotFoundException;
    }

    private static string ToolPath(string fileName) {
        string path = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.System),
            fileName);
        return File.Exists(path) ? path : fileName;
    }
}
