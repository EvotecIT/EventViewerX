using System.Diagnostics;

namespace EventViewerX.SigmaAudit;

internal static class GitCorpusVerifier {
    internal static void Verify(string corpusPath, string expectedCommit) {
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
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10000)) {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Git did not complete within 10 seconds while verifying the Sigma corpus.");
        }
        if (process.ExitCode != 0) {
            throw new InvalidDataException(
                $"Git failed while verifying the Sigma corpus: {standardError.Trim()}");
        }
        return standardOutput;
    }
}
