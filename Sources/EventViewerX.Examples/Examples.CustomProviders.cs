using EventViewerX.Providers;

namespace EventViewerX.Examples;

internal partial class Examples {
    public sealed class ScanCompletedPayload {
        [EventProviderPayloadField(0)]
        public string ComputerName { get; init; } = string.Empty;

        [EventProviderPayloadField(1)]
        public uint FindingCount { get; init; }
    }

    public static EventProviderDefinition CreateCustomProviderDefinition(
        string packageVersion = "1.0.0") {

        var provider = EventProviderDefinition.Create(
                "Contoso.Scanner",
                Guid.Parse("7a87f315-4b5e-40a2-b748-b0cdd8adab41"),
                packageVersion)
            .AddChannel(EventProviderChannelDefinition.Operational(
                "Operational",
                "Contoso.Scanner/Operational"));

        EventProviderEventDefinition scanCompleted =
            EventProviderEventDefinition.FromType<ScanCompletedPayload>(
                "ScanCompleted",
                1000,
                "Operational");
        scanCompleted.Messages["en-US"] =
            "Scan of {ComputerName} found {FindingCount} issues.";
        provider.AddEvent(scanCompleted);
        return provider;
    }

    public static EventProviderPackageBuildResult BuildCustomProvider(
        string outputPath,
        string baselinePath = "") {

        return EventProviderPackageBuilder.Build(
            CreateCustomProviderDefinition(),
            outputPath,
            new EventProviderPackageBuildOptions {
                BaselinePath = baselinePath,
                Overwrite = true
            });
    }

    public static EventProviderPackageInstallResult InstallCustomProvider(
        string packagePath,
        string trustedSignerThumbprint) {

        return EventProviderPackageManager.Install(
            packagePath,
            new EventProviderPackageInstallOptions {
                TrustMode =
                    EventProviderPackageTrustMode.RequireTrustedSignature,
                TrustedSignerThumbprints =
                    new[] { trustedSignerThumbprint }
            });
    }

    public static ManifestEventWriteResult WriteCustomProviderEvent() {
        using var writer = ResolvedManifestEventWriter.Open(
            "Contoso.Scanner",
            "ScanCompleted");
        return writer.Write(new ScanCompletedPayload {
            ComputerName = Environment.MachineName,
            FindingCount = 7
        });
    }
}
