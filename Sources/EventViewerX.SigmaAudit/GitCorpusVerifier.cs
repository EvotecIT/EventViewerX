using System.Diagnostics;

namespace EventViewerX.SigmaAudit;

internal static class GitCorpusVerifier {
    internal static GitCorpusSnapshot Verify(string corpusPath, string expectedCommit) {
        DirectoryInfo repository = FindRepository(corpusPath);
        string actualCommit = Run(repository.FullName, "rev-parse", "HEAD").Trim();
        if (!string.Equals(actualCommit, expectedCommit, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException(
                $"Sigma corpus HEAD is '{actualCommit}', not requested commit '{expectedCommit}'.");
        }

        string relativeCorpus = Path.GetRelativePath(repository.FullName, corpusPath);
        string status = Run(
            repository.FullName,
            "status",
            "--porcelain",
            "--untracked-files=no",
            "--",
            relativeCorpus).Trim();
        if (status.Length != 0) {
            throw new InvalidDataException(
                "Sigma corpus contains tracked changes and cannot produce a reproducible compatibility report.");
        }

        string trackedOutput = Run(
            repository.FullName,
            "ls-files",
            "-z",
            "--",
            relativeCorpus);
        string corpusFullPath = Path.GetFullPath(corpusPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string[] files = trackedOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.GetFullPath(Path.Combine(
                repository.FullName,
                path.Replace('/', Path.DirectorySeparatorChar))))
            .Where(path =>
                path.StartsWith(corpusFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                (path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0) {
            throw new InvalidDataException(
                $"Sigma corpus '{corpusFullPath}' contains no tracked YAML files.");
        }
        string? missing = files.FirstOrDefault(static path => !File.Exists(path));
        if (missing != null) {
            throw new InvalidDataException(
                $"Tracked Sigma corpus file '{missing}' is not materialized in the worktree.");
        }
        return new GitCorpusSnapshot(files);
    }

    private static DirectoryInfo FindRepository(string path) {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        for (DirectoryInfo? candidate = directory; candidate != null; candidate = candidate.Parent) {
            if (Directory.Exists(Path.Combine(candidate.FullName, ".git")) ||
                File.Exists(Path.Combine(candidate.FullName, ".git"))) {
                return candidate;
            }
        }
        throw new InvalidDataException(
            $"Sigma corpus '{directory.FullName}' is not inside a Git worktree.");
    }

    private static string Run(string workingDirectory, params string[] arguments) {
        var startInfo = new ProcessStartInfo {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments) {
            startInfo.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Git could not be started.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000)) {
            try {
                process.Kill(entireProcessTree: true);
                _ = process.WaitForExit(5000);
            } catch (InvalidOperationException) {
                // The process exited between the timeout and termination request.
            }
            try {
                _ = Task.WhenAll(outputTask, errorTask).Wait(TimeSpan.FromSeconds(5));
            } catch (AggregateException) {
                // Preserve the primary bounded-runtime failure below.
            }
            throw new TimeoutException("Git did not complete within 10 seconds while verifying the Sigma corpus.");
        }
        if (!Task.WhenAll(outputTask, errorTask).Wait(TimeSpan.FromSeconds(5))) {
            throw new TimeoutException("Git exited but its redirected output did not close within 5 seconds.");
        }
        string standardOutput = outputTask.GetAwaiter().GetResult();
        string standardError = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0) {
            throw new InvalidDataException(
                $"Git failed while verifying the Sigma corpus: {standardError.Trim()}");
        }
        return standardOutput;
    }
}

internal sealed class GitCorpusSnapshot {
    internal GitCorpusSnapshot(IReadOnlyList<string> files) {
        Files = files;
    }

    internal IReadOnlyList<string> Files { get; }
}
