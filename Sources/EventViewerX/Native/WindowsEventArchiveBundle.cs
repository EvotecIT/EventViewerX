namespace EventViewerX.Native;

/// <summary>Stages an EVTX and its localized resources without changing the log basename.</summary>
internal sealed class WindowsEventArchiveBundle {
    private readonly string destination;
    private bool preserveRecovery;
    internal WindowsEventArchiveBundle(string destination) {
        this.destination = destination;
        DirectoryPath = Path.Combine(Path.GetDirectoryName(destination)!,
            "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".archive");
        Directory.CreateDirectory(DirectoryPath);
        EventLogPath = Path.Combine(DirectoryPath, Path.GetFileName(destination));
    }

    internal string DirectoryPath { get; }
    internal string EventLogPath { get; }

    internal void Publish(bool includeEventLog, bool overwrite, CancellationToken cancellationToken) {
        var files = new List<(string Source, string Destination, string? Backup)>();
        var createdDirectories = new List<string>();
        string metadata = Path.Combine(DirectoryPath, "LocaleMetaData");
        if (Directory.Exists(metadata)) {
            int prefixLength = metadata.Length + 1;
            foreach (string source in Directory.EnumerateFiles(metadata, "*", SearchOption.AllDirectories)
                         .OrderBy(static path => path, StringComparer.Ordinal)) {
                files.Add((source, Path.Combine(Path.GetDirectoryName(destination)!, "LocaleMetaData",
                    source.Substring(prefixLength)), null));
            }
        }
        if (includeEventLog) {
            files.Add((EventLogPath, destination, null));
        }
        int promoted = 0;
        try {
            for (int index = 0; index < files.Count; index++) {
                cancellationToken.ThrowIfCancellationRequested();
                var file = files[index];
                if (Directory.Exists(file.Destination)) {
                    throw new IOException($"Archive output '{file.Destination}' is a directory.");
                }
                if (File.Exists(file.Destination)) {
                    if (!overwrite) {
                        throw new IOException($"Archive output '{file.Destination}' already exists.");
                    }
                    string backupDirectory = Path.Combine(DirectoryPath, "rollback");
                    Directory.CreateDirectory(backupDirectory);
                    string backup = Path.Combine(backupDirectory, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    CopyFile(file.Destination, backup, cancellationToken);
                    files[index] = (file.Source, file.Destination, backup);
                }
                CreateOutputDirectory(Path.GetDirectoryName(file.Destination)!, createdDirectories);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Publication is a commit boundary. Finish or roll back the complete file set;
            // observing cancellation between replacements would leave a mismatched archive.
            foreach (var file in files) {
                EventLogExporter.PromoteTemporaryFile(file.Source, file.Destination, overwrite);
                promoted++;
            }
        } catch (Exception failure) {
            var failures = new List<Exception> { failure };
            for (int index = promoted - 1; index >= 0; index--) {
                var file = files[index];
                try {
                    if (file.Backup != null) {
                        EventLogExporter.PromoteTemporaryFile(file.Backup, file.Destination, overwrite: true);
                    } else {
                        File.Delete(file.Destination);
                    }
                } catch (Exception rollbackFailure) when (rollbackFailure is IOException || rollbackFailure is UnauthorizedAccessException) {
                    failures.Add(rollbackFailure);
                }
            }
            if (failures.Count > 1) {
                preserveRecovery = true;
                throw new AggregateException($"Archive publication failed and some output could not be restored. Recovery copies remain in '{DirectoryPath}'.", failures);
            }
            throw;
        } finally {
            for (int index = createdDirectories.Count - 1; index >= 0; index--) {
                string directory = createdDirectories[index];
                try {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) {
                        Directory.Delete(directory);
                    }
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }
        }
    }

    private static void CreateOutputDirectory(string path, ICollection<string> createdDirectories) {
        if (Directory.Exists(path)) {
            return;
        }
        string? parent = Path.GetDirectoryName(path);
        if (parent != null) {
            CreateOutputDirectory(parent, createdDirectories);
        }
        Directory.CreateDirectory(path);
        createdDirectories.Add(path);
    }

    internal void Cleanup() {
        if (preserveRecovery) {
            return;
        }
        try {
            if (Directory.Exists(DirectoryPath)) {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        } catch (IOException) {
            // A canceled native worker keeps ownership until its late cleanup callback.
        } catch (UnauthorizedAccessException) {
            // Preserve the authoritative archive failure.
        }
    }

    private static void CopyFile(string sourcePath, string destinationPath, CancellationToken cancellationToken) {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        int count;
        while ((count = source.Read(buffer, 0, buffer.Length)) != 0) {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, count);
        }
        destination.Flush(flushToDisk: true);
    }
}
