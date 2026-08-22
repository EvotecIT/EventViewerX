using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace EventViewerX;

public static partial class CollectorSubscriptionManager {
    private static readonly TimeSpan WecUtilTimeout =
        TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RollbackTimeout =
        TimeSpan.FromSeconds(30);

    /// <summary>Creates or updates a local WEC subscription from a typed definition and verifies the result.</summary>
    public static CollectorSubscriptionSnapshot ApplyCollectorSubscription(
        CollectorSubscriptionDefinition definition,
        CancellationToken cancellationToken = default) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        return ApplyCollectorSubscription(
            definition,
            static name => GetCollectorSubscriptionSnapshot(name),
            RunWecUtil,
            cancellationToken);
    }

    internal static CollectorSubscriptionSnapshot ApplyCollectorSubscription(
        CollectorSubscriptionDefinition definition,
        Func<string, CollectorSubscriptionSnapshot?> snapshotResolver,
        Func<IReadOnlyList<string>, CancellationToken, string> wecUtilRunner,
        CancellationToken cancellationToken) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        if (snapshotResolver == null) {
            throw new ArgumentNullException(nameof(snapshotResolver));
        }
        if (wecUtilRunner == null) {
            throw new ArgumentNullException(nameof(wecUtilRunner));
        }
        string xml = definition.ToXml();
        string name = definition.SubscriptionId.Trim();
        bool exists = snapshotResolver(name) != null;
        string? previousXml = exists
            ? wecUtilRunner(new[] { "gs", name, "/f:xml" }, cancellationToken)
            : null;
        string temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"EventViewerX.Wec.{Guid.NewGuid():N}.xml");
        string? persistedXml = null;
        bool createCommandCompleted = false;
        try {
            File.WriteAllText(temporaryPath, xml, new System.Text.UTF8Encoding(false));
            try {
                wecUtilRunner(
                    exists ? new[] { "ss", $"/c:{temporaryPath}" } : new[] { "cs", temporaryPath },
                    cancellationToken);
                createCommandCompleted = !exists;
                persistedXml = wecUtilRunner(
                    new[] { "gs", name, "/f:xml" },
                    cancellationToken);
                if (!CollectorSubscriptionXml.AreEquivalent(persistedXml, xml)) {
                    throw new InvalidOperationException(
                        $"Collector subscription '{name}' did not retain the requested definition.");
                }
            } catch (Exception applyException) {
                Exception? rollbackException = TryRestoreSubscription(
                    name,
                    exists,
                    createCommandCompleted,
                    previousXml,
                    temporaryPath,
                    snapshotResolver,
                    wecUtilRunner);
                if (rollbackException != null) {
                    throw new InvalidOperationException(
                        $"Collector subscription '{name}' could not be applied and rollback also failed; its persisted state is unknown.",
                        new AggregateException(
                            applyException,
                            rollbackException));
                }
                ExceptionDispatchInfo.Capture(applyException).Throw();
                throw new InvalidOperationException(
                    "The collector subscription apply failure could not be rethrown.");
            }
        } finally {
            try {
                File.Delete(temporaryPath);
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }

        CollectorSubscriptionSnapshot snapshot =
            snapshotResolver(name) ??
            throw new InvalidOperationException(
                $"Windows reported success but collector subscription '{name}' could not be read back.");
        if (CollectorSubscriptionXml.TryNormalize(
                persistedXml!,
                out CollectorSubscriptionXmlDetails? details,
                out _)) {
            snapshot.RawXml = details!.NormalizedXml;
            snapshot.HasXml = true;
            snapshot.Description = details.Description;
            snapshot.DestinationLog = details.DestinationLog;
            snapshot.Queries = details.Queries;
            snapshot.QueryCount = details.Queries.Count;
        }
        return snapshot;
    }

    /// <summary>Writes a typed WEC definition to a UTF-8 XML file.</summary>
    public static FileInfo WriteCollectorSubscriptionDefinition(
        CollectorSubscriptionDefinition definition,
        string path,
        bool overwrite = false) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        }
        string fullPath = Path.GetFullPath(path);
        string? parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parent)) {
            Directory.CreateDirectory(parent);
        }
        if (!overwrite && File.Exists(fullPath)) {
            throw new IOException($"File '{fullPath}' already exists.");
        }
        File.WriteAllText(fullPath, definition.ToXml(), new System.Text.UTF8Encoding(false));
        return new FileInfo(fullPath);
    }

    private static string RunWecUtil(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) {

        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "wecutil.exe");
        if (!File.Exists(executable)) {
            throw new FileNotFoundException("Windows Event Collector utility was not found.", executable);
        }
        var startInfo = new ProcessStartInfo {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) {
#if NET472
            startInfo.Arguments += startInfo.Arguments.Length == 0
                ? QuoteProcessArgument(argument)
                : " " + QuoteProcessArgument(argument);
#else
            startInfo.ArgumentList.Add(argument);
#endif
        }
        return BoundedProcessRunner.Run(
            startInfo,
            WecUtilTimeout,
            cancellationToken);
    }

    private static Exception? TryRestoreSubscription(
        string name,
        bool existed,
        bool createCommandCompleted,
        string? previousXml,
        string temporaryPath,
        Func<string, CollectorSubscriptionSnapshot?> snapshotResolver,
        Func<IReadOnlyList<string>, CancellationToken, string> wecUtilRunner) {

        try {
            using var rollbackCancellation =
                new CancellationTokenSource(RollbackTimeout);
            if (!existed) {
                if (!createCommandCompleted) {
                    for (int attempt = 0; attempt < 20; attempt++) {
                        if (snapshotResolver(name) != null) {
                            throw new InvalidOperationException(
                                $"Rollback cannot safely delete collector subscription '{name}' because the create command failed before ownership was established and a same-name subscription now exists.");
                        }
                        rollbackCancellation.Token.ThrowIfCancellationRequested();
                        if (rollbackCancellation.Token.WaitHandle.WaitOne(100)) {
                            rollbackCancellation.Token.ThrowIfCancellationRequested();
                        }
                    }
                    return null;
                }
                wecUtilRunner(
                    new[] { "ds", name },
                    rollbackCancellation.Token);
                for (int attempt = 0; attempt < 20; attempt++) {
                    if (snapshotResolver(name) == null) {
                        return null;
                    }
                    rollbackCancellation.Token.ThrowIfCancellationRequested();
                    if (rollbackCancellation.Token.WaitHandle.WaitOne(100)) {
                        rollbackCancellation.Token.ThrowIfCancellationRequested();
                    }
                }
                throw new InvalidOperationException(
                    $"Rollback could not confirm deletion of collector subscription '{name}'.");
            }
            if (string.IsNullOrWhiteSpace(previousXml)) {
                throw new InvalidOperationException(
                    $"Rollback cannot restore collector subscription '{name}' because its previous XML definition is unavailable.");
            }
            File.WriteAllText(
                temporaryPath,
                previousXml,
                new System.Text.UTF8Encoding(false));
            wecUtilRunner(
                new[] { "ss", $"/c:{temporaryPath}" },
                rollbackCancellation.Token);
            string restoredXml = wecUtilRunner(
                new[] { "gs", name, "/f:xml" },
                rollbackCancellation.Token);
            if (!CollectorSubscriptionXml.AreEquivalent(
                    restoredXml,
                    previousXml)) {
                throw new InvalidOperationException(
                    $"Rollback could not verify the restored definition for collector subscription '{name}'.");
            }
            return null;
        } catch (Exception exception) {
            return exception;
        }
    }

#if NET472
    private static string QuoteProcessArgument(string argument) {
        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) {
            return argument;
        }

        var result = new StringBuilder(argument.Length + 2).Append('"');
        int backslashes = 0;
        foreach (char character in argument) {
            if (character == '\\') {
                backslashes++;
                continue;
            }
            if (character == '"') {
                result.Append('\\', backslashes * 2 + 1).Append(character);
                backslashes = 0;
                continue;
            }
            result.Append('\\', backslashes).Append(character);
            backslashes = 0;
        }
        result.Append('\\', backslashes * 2).Append('"');
        return result.ToString();
    }
#endif
}
