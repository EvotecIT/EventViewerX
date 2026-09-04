using System.Reflection;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static int Version() {
        Console.WriteLine(GetVersion());
        return 0;
    }

    private static string GetVersion() {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(Program).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion)) {
            int buildMetadata = informationalVersion.IndexOf('+');
            return buildMetadata >= 0
                ? informationalVersion[..buildMetadata]
                : informationalVersion;
        }

        Version? assemblyVersion = assembly.GetName().Version;
        return assemblyVersion is null
            ? "unknown"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }
}
