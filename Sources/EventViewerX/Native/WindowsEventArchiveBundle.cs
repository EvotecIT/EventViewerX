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
        using var ownership = FilePublication.AcquireOwnership(destination, cancellationToken);
        var files = new List<(string? Source, string Destination, string? Backup)>();
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
            // A new EVTX replaces the log generation. Resources absent from this
            // export belong to the old generation, including resource-free exports.
            string outputMetadata = Path.Combine(Path.GetDirectoryName(destination)!, "LocaleMetaData");
            if (Directory.Exists(outputMetadata)) {
                var replacementPaths = new HashSet<string>(files.Select(static file => file.Destination), FileSystemPathIdentity.Comparer);
                foreach (string oldResource in Directory.EnumerateFiles(outputMetadata, "*", SearchOption.AllDirectories)
                             .OrderBy(static path => path, StringComparer.Ordinal)) {
                    if (OwnsResource(oldResource) && !replacementPaths.Contains(oldResource)) {
                        files.Add((null, oldResource, null));
                    }
                }
            }
            files.Add((EventLogPath, destination, null));
        }
        int attempted = 0;
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
                    if (file.Source != null) {
                        CopyFile(file.Destination, backup, cancellationToken);
                    }
                    files[index] = (file.Source, file.Destination, backup);
                }
                CreateOutputDirectory(Path.GetDirectoryName(file.Destination)!, createdDirectories);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Publication is a commit boundary. Finish or roll back the complete file set;
            // observing cancellation between replacements would leave a mismatched archive.
            foreach (var file in files) {
                attempted++;
                if (file.Source == null) {
                    // Preserve the original file's streams, ACLs and timestamps.
                    // Its rollback path is reserved in preflight, but is not a byte copy.
                    File.Move(file.Destination, file.Backup!);
                } else {
                    FilePublication.Promote(file.Source, file.Destination, overwrite);
                }
            }
        } catch (Exception failure) {
            var failures = new List<Exception> { failure };
            preserveRecovery = failure is FilePublicationRecoveryException;
            // Include the member whose operation threw: native replacement can
            // change the destination even when the operation reports failure.
            for (int index = attempted - 1; index >= 0; index--) {
                var file = files[index];
                try {
                    if (file.Source == null) {
                        if (file.Backup != null && File.Exists(file.Backup)) {
                            File.Move(file.Backup, file.Destination);
                        } else if (!File.Exists(file.Destination)) {
                            throw new IOException($"Retired resource '{file.Destination}' could not be located for recovery.");
                        }
                    } else if (file.Backup != null) {
                        if (!ContentsEqual(file.Backup, file.Destination)) {
                            FilePublication.Promote(file.Backup, file.Destination, overwrite: true);
                        }
                    } else if (file.Source != null && !File.Exists(file.Source)) {
                        File.Delete(file.Destination);
                    }
                } catch (Exception rollbackFailure) when (rollbackFailure is IOException || rollbackFailure is UnauthorizedAccessException) {
                    failures.Add(rollbackFailure);
                }
            }
            if (failures.Count > 1 || preserveRecovery) {
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

    private bool OwnsResource(string path) {
        string name = Path.GetFileName(path);
        if (!name.EndsWith(".MTA", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        string logName = Path.GetFileName(destination);
        string stem = Path.GetFileNameWithoutExtension(destination);
        foreach (string prefix in new[] { logName + "_", stem + "_" }) {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                string locale = name.Substring(prefix.Length, name.Length - prefix.Length - 4);
                if (locale.Length > 0 && locale.All(static character => character >= '0' && character <= '9')) {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool ContentsEqual(string backup, string destination) {
        if (!File.Exists(destination)) {
            return false;
        }
        using var original = File.OpenRead(backup);
        using var current = File.OpenRead(destination);
        if (original.Length != current.Length) {
            return false;
        }
        var first = new byte[81920];
        var second = new byte[first.Length];
        int count;
        while ((count = original.Read(first, 0, first.Length)) > 0) {
            int remaining = count;
            int offset = 0;
            while (remaining > 0) {
                int read = current.Read(second, offset, remaining);
                if (read == 0) {
                    return false;
                }
                offset += read;
                remaining -= read;
            }
            for (int index = 0; index < count; index++) {
                if (first[index] != second[index]) {
                    return false;
                }
            }
        }
        return true;
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
