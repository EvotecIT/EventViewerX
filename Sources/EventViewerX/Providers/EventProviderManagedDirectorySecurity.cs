using System.Security.AccessControl;
using System.Security.Principal;

namespace EventViewerX.Providers;

internal static class EventProviderManagedDirectorySecurity {
    private const string ManagedRootMarker =
        ".eventviewerx-provider-root";
    private const string ManagedRootClaimLock =
        ".eventviewerx-provider-root.lock";
    private const int FullControlMask = 0x1F01FF;
    private const int ReadAndExecuteMask = 0x1200A9;

    private static readonly IReadOnlyDictionary<string, int>
        ExpectedAccess = new Dictionary<string, int>(
            StringComparer.Ordinal) {
            ["S-1-5-18"] = FullControlMask,
            ["S-1-5-32-544"] = FullControlMask,
            ["S-1-5-19"] = ReadAndExecuteMask,
            ["S-1-5-32-545"] = ReadAndExecuteMask
        };

    internal static void EnsureManagedRoot(
        string rootPath,
        TimeSpan timeout) {

        string root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(root);
        string marker = Path.Combine(
            root,
            ManagedRootMarker);
        string claimLock = Path.Combine(
            root,
            ManagedRootClaimLock);
        try {
            using FileStream claim =
                AcquireRootClaim(
                    claimLock,
                    timeout);
            if (!File.Exists(marker)) {
                string[] unrelated =
                    Directory.EnumerateFileSystemEntries(root)
                        .Where(path =>
                            !string.Equals(
                                path,
                                claimLock,
                                StringComparison.OrdinalIgnoreCase) &&
                            !IsManagedProviderDirectory(path))
                        .ToArray();
                if (unrelated.Length > 0) {
                    throw new InvalidOperationException(
                        $"Provider root '{root}' contains unrelated content and cannot be claimed as an EventViewerX-managed security boundary: " +
                        string.Join(
                            ", ",
                            unrelated.Select(Path.GetFileName)));
                }
                File.WriteAllText(
                    marker,
                    "EventViewerX managed provider root" +
                    Environment.NewLine,
                    new UTF8Encoding(false));
            }
            EnsureExact(root, timeout);
        } finally {
            try {
                File.Delete(claimLock);
            } catch (Exception) {
            }
        }
    }

    private static FileStream AcquireRootClaim(
        string claimLock,
        TimeSpan timeout) {

        if (timeout < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Root claim timeout must be greater than or equal to zero.");
        }
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (true) {
            try {
                return new FileStream(
                    claimLock,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            } catch (IOException exception) {
                if (DateTime.UtcNow >= deadline) {
                    throw new TimeoutException(
                        $"Timed out claiming managed provider root '{Path.GetDirectoryName(claimLock)}'.",
                        exception);
                }
                Thread.Sleep(50);
            }
        }
    }

    internal static void EnsureExact(
        string directoryPath,
        TimeSpan timeout) {

        string directory = Path.GetFullPath(
            directoryPath);
        Directory.CreateDirectory(directory);
        RejectReparsePoints(directory);

        RunIcacls(
            new[] {
                directory,
                "/reset",
                "/C",
                "/Q"
            },
            directory,
            timeout,
            "Provider resource ACL reset");
        RunIcacls(
            new[] {
                directory,
                "/inheritance:r",
                "/grant:r",
                "*S-1-5-18:(OI)(CI)(F)",
                "*S-1-5-32-544:(OI)(CI)(F)",
                "*S-1-5-19:(OI)(CI)(RX)",
                "*S-1-5-32-545:(OI)(CI)(RX)",
                "/C",
                "/Q"
            },
            directory,
            timeout,
            "Provider resource ACL assignment");

        if (Directory.EnumerateFileSystemEntries(
                directory).Any()) {
            RunIcacls(
                new[] {
                    Path.Combine(directory, "*"),
                    "/reset",
                    "/T",
                    "/C",
                    "/Q"
                },
                directory,
                timeout,
                "Provider child ACL reset");
        }
        VerifyExact(directory, timeout);
    }

    private static void VerifyExact(
        string directory,
        TimeSpan timeout) {

        string aclPath = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-Acl-" +
            Guid.NewGuid().ToString("N") +
            ".txt");
        try {
            RunIcacls(
                new[] {
                    directory,
                    "/save",
                    aclPath,
                    "/T",
                    "/C",
                    "/Q"
                },
                directory,
                timeout,
                "Provider resource ACL verification export");
            string[] lines = File.ReadAllLines(
                    aclPath,
                    Encoding.Unicode)
                .Where(static line =>
                    !string.IsNullOrWhiteSpace(line))
                .ToArray();
            string[] descriptors = lines
                .Where(static line =>
                    line.StartsWith(
                        "D:",
                        StringComparison.Ordinal))
                .ToArray();
            int expectedCount = 1 +
                                Directory
                                    .EnumerateFileSystemEntries(
                                        directory,
                                        "*",
                                        SearchOption.AllDirectories)
                                    .Count();
            if (descriptors.Length != expectedCount) {
                throw new UnauthorizedAccessException(
                    $"ACL verification exported {descriptors.Length} security descriptors for {expectedCount} managed paths.");
            }
            foreach (string descriptor in descriptors) {
                VerifyDescriptor(descriptor);
            }
        } finally {
            if (File.Exists(aclPath)) {
                File.Delete(aclPath);
            }
        }
    }

    private static void VerifyDescriptor(string sddl) {
        var descriptor =
            new RawSecurityDescriptor(sddl);
        if (descriptor.DiscretionaryAcl == null ||
            descriptor.DiscretionaryAcl.Count !=
            ExpectedAccess.Count) {
            throw new UnauthorizedAccessException(
                "Managed provider ACL contains an unexpected number of access rules.");
        }
        var actual = new Dictionary<string, int>(
            StringComparer.Ordinal);
        foreach (GenericAce ace in
                 descriptor.DiscretionaryAcl) {
            if (ace is not QualifiedAce qualified ||
                qualified.AceQualifier !=
                AceQualifier.AccessAllowed ||
                qualified.SecurityIdentifier == null ||
                actual.ContainsKey(
                    qualified.SecurityIdentifier.Value)) {
                throw new UnauthorizedAccessException(
                    "Managed provider ACL contains an unexpected or duplicate access rule.");
            }
            actual.Add(
                qualified.SecurityIdentifier.Value,
                qualified.AccessMask);
        }
        foreach (KeyValuePair<string, int> expected in
                 ExpectedAccess) {
            if (!actual.TryGetValue(
                    expected.Key,
                    out int mask) ||
                mask != expected.Value) {
                throw new UnauthorizedAccessException(
                    $"Managed provider ACL does not grant the exact expected access to {expected.Key}.");
            }
        }
    }

    private static void RejectReparsePoints(
        string directory) {

        IEnumerable<string> paths =
            new[] { directory }.Concat(
                Directory.EnumerateFileSystemEntries(
                    directory,
                    "*",
                    SearchOption.AllDirectories));
        string? reparsePoint = paths.FirstOrDefault(
            path =>
                (File.GetAttributes(path) &
                 FileAttributes.ReparsePoint) != 0);
        if (reparsePoint != null) {
            throw new InvalidDataException(
                $"Managed provider tree cannot contain reparse point '{reparsePoint}'.");
        }
    }

    internal static bool IsManagedProviderDirectory(
        string path) {

        try {
            if (!Directory.Exists(path) ||
                (File.GetAttributes(path) &
                 FileAttributes.ReparsePoint) != 0) {
                return false;
            }
            string name = Path.GetFileName(path);
            if (name.Length != 32 ||
                !Guid.TryParseExact(
                    name,
                    "N",
                    out Guid providerId)) {
                return false;
            }
            EventProviderInstallationState? state =
                EventProviderInstallationStore.Load(path);
            if (state == null ||
                state.ProviderId != providerId ||
                string.IsNullOrWhiteSpace(
                    state.ProviderName) ||
                string.IsNullOrWhiteSpace(
                    state.PackageSha256)) {
                return false;
            }
            string activeDirectoryName =
                string.IsNullOrWhiteSpace(
                    state.ActiveDirectoryName)
                    ? state.ActiveVersion
                    : state.ActiveDirectoryName;
            if (string.IsNullOrWhiteSpace(
                    activeDirectoryName) ||
                !string.Equals(
                    Path.GetFileName(
                        activeDirectoryName),
                    activeDirectoryName,
                    StringComparison.Ordinal) ||
                activeDirectoryName.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0) {
                return false;
            }
            string archivePath = Path.Combine(
                path,
                activeDirectoryName,
                EventProviderInstallationStore
                    .ArchivedPackageFileName);
            if (!File.Exists(archivePath) ||
                !string.Equals(
                    EventProviderHash.FileSha256(
                        archivePath),
                    state.PackageSha256,
                    StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            using EventProviderPackage package =
                EventProviderPackageReader.Open(
                    archivePath);
            return package.Definition.Id ==
                       state.ProviderId &&
                   string.Equals(
                       package.Definition.Name,
                       state.ProviderName,
                       StringComparison.Ordinal);
        } catch (Exception exception)
            when (EventProviderPackageManager
                .IsUnreadableRetainedPackage(
                    exception)) {
            return false;
        }
    }

    private static void RunIcacls(
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        string operation) {

        EventProviderProcessResult result =
            EventProviderProcessRunner.Run(
                ToolPath(),
                arguments,
                workingDirectory,
                timeout);
        EventProviderProcessRunner.EnsureSuccess(
            result,
            operation);
    }

    private static string ToolPath() {
        string path = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.System),
            "icacls.exe");
        return File.Exists(path)
            ? path
            : "icacls.exe";
    }
}