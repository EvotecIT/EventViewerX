using System.Security.Cryptography;
using System.Diagnostics;
using DBAClientX;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    private static readonly TimeSpan MaintenanceLockTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Runs SQLite integrity validation and reports supported schema and size evidence.</summary>
    public async Task<EventStoreIntegrityResult> CheckIntegrityAsync(
        CancellationToken cancellationToken = default) {

        long databaseBytes = GetDatabaseBytes(Path);
        if (!File.Exists(Path)) {
            return CreateUnhealthyIntegrityResult(
                "The EventStore database file does not exist.",
                databaseBytes);
        }
        if (databaseBytes == 0) {
            return CreateUnhealthyIntegrityResult(
                "The EventStore database file is empty.",
                databaseBytes);
        }

        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        try {
            IReadOnlyList<string> checks = await sqlite.QueryReadOnlyAsListAsync(
                Path,
                "PRAGMA integrity_check;",
                static record => record.GetString(0),
                cancellationToken: cancellationToken,
                busyTimeoutMs: 10000).ConfigureAwait(false);
            var diagnostics = checks
                .Where(static value => !string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase))
                .ToList();
            IReadOnlyList<string> tables = await sqlite.QueryReadOnlyAsListAsync(
                Path,
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name IN (" +
                "'evx_store_metadata', 'evx_definitions', 'evx_events', 'evx_checkpoints', " +
                "'evx_findings', 'evx_finding_evidence', 'evx_finding_entities');",
                static record => record.GetString(0),
                cancellationToken: cancellationToken,
                busyTimeoutMs: 10000).ConfigureAwait(false);
            var availableTables = new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);
            string[] requiredTables = StoreSchemaContracts
                .Select(static contract => contract.TableName)
                .ToArray();
            foreach (string requiredTable in requiredTables.Where(table => !availableTables.Contains(table))) {
                diagnostics.Add($"Required EventStore table '{requiredTable}' is missing.");
            }
            if (diagnostics.Count > 0) {
                return new EventStoreIntegrityResult(
                    false,
                    diagnostics,
                    0,
                    0,
                    0,
                    0,
                    0,
                    databaseBytes);
            }
            await ValidateStoreSchemaAsync(
                sqlite,
                diagnostics,
                cancellationToken).ConfigureAwait(false);
            if (diagnostics.Count > 0) {
                return new EventStoreIntegrityResult(
                    false,
                    diagnostics,
                    0,
                    0,
                    0,
                    0,
                    0,
                    databaseBytes);
            }

            IReadOnlyList<StoreVersionRow> versions = await sqlite.QueryReadOnlyAsListAsync(
                Path,
                "SELECT schema_version, event_identity_version, finding_schema_version FROM evx_store_metadata WHERE singleton_id = 1;",
                static record => new StoreVersionRow(record.GetInt32(0), record.GetInt32(1), record.GetInt32(2)),
                cancellationToken: cancellationToken,
                busyTimeoutMs: 10000).ConfigureAwait(false);
            if (versions.Count != 1) {
                diagnostics.Add(
                    $"EventStore metadata must contain exactly one singleton row; observed {versions.Count}.");
                return new EventStoreIntegrityResult(
                    false,
                    diagnostics,
                    0,
                    0,
                    0,
                    0,
                    0,
                    databaseBytes);
            }

            StoreVersionRow version = versions[0];
            if (version.SchemaVersion != SchemaVersion) {
                diagnostics.Add($"Unsupported base schema version {version.SchemaVersion}.");
            }
            if (version.EventIdentityVersion != 3) {
                diagnostics.Add($"Unsupported event identity version {version.EventIdentityVersion}.");
            }
            if (version.FindingSchemaVersion != 2) {
                diagnostics.Add($"Unsupported finding schema version {version.FindingSchemaVersion}.");
            }
            long events = (await sqlite.QueryReadOnlyAsListAsync(
                Path,
                "SELECT COUNT(*) FROM evx_events;",
                static record => Convert.ToInt64(record.GetValue(0), CultureInfo.InvariantCulture),
                cancellationToken: cancellationToken,
                busyTimeoutMs: 10000).ConfigureAwait(false)).Single();
            long findings = (await sqlite.QueryReadOnlyAsListAsync(
                Path,
                "SELECT COUNT(*) FROM evx_findings;",
                static record => Convert.ToInt64(record.GetValue(0), CultureInfo.InvariantCulture),
                cancellationToken: cancellationToken,
                busyTimeoutMs: 10000).ConfigureAwait(false)).Single();
            return new EventStoreIntegrityResult(
                diagnostics.Count == 0,
                diagnostics,
                version.SchemaVersion,
                version.EventIdentityVersion,
                version.FindingSchemaVersion,
                events,
                findings,
                databaseBytes);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) {
            return CreateUnhealthyIntegrityResult(
                $"The EventStore database could not be validated read-only: {exception.Message}",
                databaseBytes);
        }
    }

    /// <summary>Creates a transactionally consistent, integrity-checked SQLite backup with a checksum.</summary>
    public async Task<EventStoreBackupResult> BackupAsync(
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(destinationPath)) {
            throw new ArgumentException("Backup destination cannot be empty.", nameof(destinationPath));
        }
        EventStoreIntegrityResult sourceIntegrity = await CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);
        if (!sourceIntegrity.IsHealthy) {
            throw new InvalidDataException(
                "Source EventStore failed integrity validation: " + string.Join(" ", sourceIntegrity.Diagnostics));
        }
        string destination = System.IO.Path.GetFullPath(destinationPath);
        if (FileSystemPathIdentity.Equals(destination, Path)) {
            throw new ArgumentException("Backup destination must differ from the live store.", nameof(destinationPath));
        }
        string? directory = System.IO.Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory!);
        }
        if (File.Exists(destination) && !overwrite) {
            throw new IOException($"Backup destination '{destination}' already exists.");
        }
        string pending = destination + ".pending-" + Guid.NewGuid().ToString("N");
        try {
            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            await using (SQLiteAsyncSession session = await sqlite
                    .OpenSessionAsync(Path, cancellationToken)
                    .ConfigureAwait(false)) {
                await session.ExecuteNonQueryAsync(
                    "VACUUM INTO $destination;",
                    new Dictionary<string, object?> { ["$destination"] = pending },
                    cancellationToken).ConfigureAwait(false);
            }
            EventStoreIntegrityResult integrity = await new EventStore(pending)
                .CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);
            if (!integrity.IsHealthy) {
                throw new InvalidDataException(
                    "Generated EventStore backup failed integrity validation: " + string.Join(" ", integrity.Diagnostics));
            }
            if (!overwrite) {
                File.Move(pending, destination);
            } else {
                PublishOverwritingBackup(pending, destination);
            }
            return new EventStoreBackupResult(
                destination,
                GetDatabaseBytes(destination),
                ComputeSha256(destination),
                DateTime.UtcNow);
        } finally {
            if (File.Exists(pending)) {
                File.Delete(pending);
            }
        }
    }

    /// <summary>
    /// Replaces this store from an integrity-checked backup. Callers must stop active readers and writers first;
    /// the original database is restored automatically if post-replacement validation fails.
    /// </summary>
    public async Task<EventStoreIntegrityResult> RestoreAsync(
        string backupPath,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(backupPath)) {
            throw new ArgumentException("Backup path cannot be empty.", nameof(backupPath));
        }
        string backup = System.IO.Path.GetFullPath(backupPath);
        if (!File.Exists(backup)) {
            throw new FileNotFoundException("EventStore backup was not found.", backup);
        }
        if (FileSystemPathIdentity.Equals(backup, Path)) {
            throw new ArgumentException("Backup path must differ from the live store.", nameof(backupPath));
        }
        string? targetDirectory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(targetDirectory)) {
            Directory.CreateDirectory(targetDirectory!);
        }
        using FileStream maintenance = await AcquireMaintenanceLockAsync(cancellationToken).ConfigureAwait(false);
        string pending = Path + ".restore-pending-" + Guid.NewGuid().ToString("N");
        string recovery = Path + ".restore-recovery-" + Guid.NewGuid().ToString("N");
        bool liveStoreExisted = File.Exists(Path);
        bool mutationStarted = false;
        try {
            await CreateConsistentSnapshotAsync(
                backup,
                pending,
                cancellationToken).ConfigureAwait(false);
            EventStoreIntegrityResult backupIntegrity = await new EventStore(pending)
                .CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);
            if (!backupIntegrity.IsHealthy) {
                throw new InvalidDataException(
                    "EventStore backup failed integrity validation: " + string.Join(" ", backupIntegrity.Diagnostics));
            }
            if (liveStoreExisted) {
                await CreateConsistentSnapshotAsync(
                    Path,
                    recovery,
                    cancellationToken).ConfigureAwait(false);
                EventStoreIntegrityResult recoveryIntegrity = await new EventStore(recovery)
                    .CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);
                if (!recoveryIntegrity.IsHealthy) {
                    throw new InvalidDataException(
                        "Live EventStore recovery snapshot failed integrity validation: " +
                        string.Join(" ", recoveryIntegrity.Diagnostics));
                }
                cancellationToken.ThrowIfCancellationRequested();
                mutationStarted = true;
                DeleteSidecar(Path + "-wal");
                DeleteSidecar(Path + "-shm");
                File.Replace(pending, Path, destinationBackupFileName: null);
            } else {
                mutationStarted = true;
                File.Move(pending, Path);
            }
            lock (_initializationLock) {
                _initialized = false;
            }
            EventStoreIntegrityResult restored = await CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);
            if (!restored.IsHealthy) {
                throw new InvalidDataException(
                    "Restored EventStore failed integrity validation: " + string.Join(" ", restored.Diagnostics));
            }
            DeleteSidecar(recovery);
            return restored;
        } catch {
            if (mutationStarted && liveStoreExisted && File.Exists(recovery)) {
                DeleteSidecar(Path + "-wal");
                DeleteSidecar(Path + "-shm");
                if (File.Exists(Path)) {
                    File.Replace(recovery, Path, destinationBackupFileName: null);
                } else {
                    File.Move(recovery, Path);
                }
                lock (_initializationLock) {
                    _initialized = false;
                }
            } else if (mutationStarted && !liveStoreExisted) {
                DeleteSidecar(Path + "-wal");
                DeleteSidecar(Path + "-shm");
                DeleteSidecar(Path);
                lock (_initializationLock) {
                    _initialized = false;
                }
            }
            throw;
        } finally {
            DeleteSidecar(pending);
            DeleteSidecar(recovery);
        }
    }

    /// <summary>
    /// Creates a standalone SQLite snapshot that includes committed WAL state and can be moved without sidecars.
    /// </summary>
    internal static async Task CreateConsistentSnapshotAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default) {

        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await sqlite.BackupDatabaseIncrementalAsync(
            sourcePath,
            destinationPath,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Applies explicit event and finding retention with optional SQLite compaction.</summary>
    public async Task<EventStoreRetentionResult> ApplyRetentionAsync(
        EventStoreRetentionPolicy policy,
        DateTime? nowUtc = null,
        CancellationToken cancellationToken = default) {

        if (policy == null) {
            throw new ArgumentNullException(nameof(policy));
        }
        EventStoreRetentionPolicy snapshot = policy.Snapshot();
        DateTime now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();
        long beforeBytes = GetDatabaseBytes(Path);
        int deletedEvents = snapshot.EventRetention.HasValue
            ? await PruneBeforeAsync(now - snapshot.EventRetention.Value, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
            : 0;
        int deletedFindings = snapshot.FindingRetention.HasValue
            ? await PruneFindingsBeforeAsync(now - snapshot.FindingRetention.Value, cancellationToken)
                .ConfigureAwait(false)
            : 0;
        if (snapshot.VacuumAfterPrune && (deletedEvents > 0 || deletedFindings > 0)) {
            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            await using SQLiteAsyncSession session = await sqlite
                .OpenSessionAsync(Path, cancellationToken)
                .ConfigureAwait(false);
            await session.ExecuteNonQueryAsync("VACUUM;", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        return new EventStoreRetentionResult(
            deletedEvents,
            deletedFindings,
            beforeBytes,
            GetDatabaseBytes(Path),
            snapshot.VacuumAfterPrune && (deletedEvents > 0 || deletedFindings > 0),
            DateTime.UtcNow);
    }

    private async Task<int> PruneFindingsBeforeAsync(DateTime before, CancellationToken cancellationToken) {
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        return await session.ExecuteNonQueryAsync(
            "DELETE FROM evx_findings WHERE end_time_utc < $before;",
            new Dictionary<string, object?> {
                ["$before"] = before.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<FileStream> AcquireMaintenanceLockAsync(CancellationToken cancellationToken) {
        string lockPath = Path + ".maintenance.lock";
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } catch (IOException) when (stopwatch.Elapsed < MaintenanceLockTimeout) {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static long GetDatabaseBytes(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    private static EventStoreIntegrityResult CreateUnhealthyIntegrityResult(
        string diagnostic,
        long databaseBytes) => new(
            false,
            new[] { diagnostic },
            0,
            0,
            0,
            0,
            0,
            databaseBytes);

    private static string ComputeSha256(string path) {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
    }

    private static void ReplaceDatabaseWithoutSidecars(string source, string destination) {
        string quarantineSuffix = ".replace-quarantine-" + Guid.NewGuid().ToString("N");
        var quarantined = new List<(string Original, string Quarantine)>();
        bool published = false;
        try {
            foreach (string sidecar in new[] { destination + "-wal", destination + "-shm" }) {
                if (!File.Exists(sidecar)) {
                    continue;
                }
                string quarantine = sidecar + quarantineSuffix;
                File.Move(sidecar, quarantine);
                quarantined.Add((sidecar, quarantine));
            }
            File.Replace(source, destination, destinationBackupFileName: null);
            published = true;
        } finally {
            foreach ((string original, string quarantine) in quarantined) {
                if (!published && !File.Exists(original) && File.Exists(quarantine)) {
                    File.Move(quarantine, original);
                } else {
                    DeleteSidecar(quarantine);
                }
            }
        }
    }

    private static void PublishOverwritingBackup(string source, string destination) {
        if (File.Exists(destination)) {
            ReplaceDatabaseWithoutSidecars(source, destination);
            return;
        }
        try {
            File.Move(source, destination);
        } catch (IOException) when (File.Exists(destination)) {
            ReplaceDatabaseWithoutSidecars(source, destination);
        }
    }

    private static void DeleteSidecar(string path) {
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }

    private sealed class StoreVersionRow {
        internal StoreVersionRow(int schemaVersion, int eventIdentityVersion, int findingSchemaVersion) {
            SchemaVersion = schemaVersion;
            EventIdentityVersion = eventIdentityVersion;
            FindingSchemaVersion = findingSchemaVersion;
        }

        internal int SchemaVersion { get; }
        internal int EventIdentityVersion { get; }
        internal int FindingSchemaVersion { get; }
    }
}
