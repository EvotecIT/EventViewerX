using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace EventViewerX;

/// <summary>Represents one persisted event-log checkpoint and its log-generation identity.</summary>
public sealed class EventCheckpointValue {
    internal EventCheckpointValue(long? recordId, Guid generationId, string? boundaryIdentity) {
        RecordId = recordId;
        GenerationId = generationId;
        BoundaryIdentity = boundaryIdentity;
    }

    /// <summary>Last contiguously processed record identifier, or <c>null</c> before the first record in a generation.</summary>
    public long? RecordId { get; }

    /// <summary>Identity used to reject stale writers after a log is cleared or replaced.</summary>
    public Guid GenerationId { get; }

    /// <summary>Identity of the event stored at <see cref="RecordId"/>, used to detect a cleared or replaced source.</summary>
    public string? BoundaryIdentity { get; }
}

/// <summary>Immutable snapshot of checkpoints loaded from a checkpoint file.</summary>
public sealed class EventCheckpointSnapshot {
    private readonly Dictionary<string, EventCheckpointValue> _checkpoints;
    private readonly Dictionary<string, long> _records;
    private readonly ReadOnlyDictionary<string, EventCheckpointValue> _checkpointView;
    private readonly ReadOnlyDictionary<string, long> _recordView;

    internal EventCheckpointSnapshot(string checkpointPath, Dictionary<string, EventCheckpointValue> checkpoints) {
        CheckpointPath = checkpointPath;
        StatePath = EventCheckpointStore.GetStateFilePath(checkpointPath);
        LockPath = EventCheckpointStore.GetLockFilePath(checkpointPath);
        _checkpoints = new Dictionary<string, EventCheckpointValue>(checkpoints, StringComparer.OrdinalIgnoreCase);
        _records = checkpoints
            .Where(static entry => entry.Value.RecordId.HasValue)
            .ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value.RecordId!.Value,
                StringComparer.OrdinalIgnoreCase);
        _checkpointView = new ReadOnlyDictionary<string, EventCheckpointValue>(_checkpoints);
        _recordView = new ReadOnlyDictionary<string, long>(_records);
    }

    /// <summary>Numeric checkpoint values mirrored to the compatibility JSON file on a best-effort basis.</summary>
    public IReadOnlyDictionary<string, long> Records => _recordView;

    /// <summary>Requested compatibility checkpoint file used by PowerShell and legacy clients.</summary>
    public string CheckpointPath { get; }

    /// <summary>Authoritative generation-aware companion state file.</summary>
    public string StatePath { get; }

    /// <summary>Cross-process lock file used while loading, updating, or resetting the checkpoint.</summary>
    public string LockPath { get; }

    /// <summary>All generation-aware checkpoints, including generations that have not emitted a record yet.</summary>
    public IReadOnlyDictionary<string, EventCheckpointValue> Checkpoints => _checkpointView;

    /// <summary>Gets a checkpoint, including its generation identity.</summary>
    public bool TryGetValue(string key, out EventCheckpointValue? value) {
        if (string.IsNullOrWhiteSpace(key)) {
            value = null;
            return false;
        }

        return _checkpoints.TryGetValue(key, out value);
    }

    internal Dictionary<string, EventCheckpointValue> CopyValues()
        => new(_checkpoints, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Describes a compare-and-advance checkpoint update.</summary>
public sealed class EventCheckpointUpdate {
    /// <summary>Creates a checkpoint update.</summary>
    /// <param name="key">Checkpoint key.</param>
    /// <param name="recordId">Last contiguously processed record, or <c>null</c> to start a generation without progress.</param>
    /// <param name="expectedGenerationId">Generation observed when the query began.</param>
    /// <param name="startsNewGeneration">Whether the source was detected as cleared or replaced.</param>
    /// <param name="boundaryIdentity">Identity of the event stored at <paramref name="recordId"/>.</param>
    public EventCheckpointUpdate(
        string key,
        long? recordId,
        Guid expectedGenerationId,
        bool startsNewGeneration = false,
        string? boundaryIdentity = null) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("Checkpoint key cannot be null or empty.", nameof(key));
        }
        if (recordId < 0) {
            throw new ArgumentOutOfRangeException(nameof(recordId), "Checkpoint record ID must be greater than or equal to zero.");
        }
        if (!recordId.HasValue && !string.IsNullOrWhiteSpace(boundaryIdentity)) {
            throw new ArgumentException("A checkpoint boundary identity requires a record ID.", nameof(boundaryIdentity));
        }

        Key = key;
        RecordId = recordId;
        ExpectedGenerationId = expectedGenerationId;
        StartsNewGeneration = startsNewGeneration;
        BoundaryIdentity = string.IsNullOrWhiteSpace(boundaryIdentity) ? null : boundaryIdentity!.Trim();
    }

    /// <summary>Checkpoint key.</summary>
    public string Key { get; }

    /// <summary>Last contiguously processed record, if any.</summary>
    public long? RecordId { get; }

    /// <summary>Generation observed when the query began.</summary>
    public Guid ExpectedGenerationId { get; }

    /// <summary>Whether this update starts a new log generation.</summary>
    public bool StartsNewGeneration { get; }

    /// <summary>Identity of the event stored at <see cref="RecordId"/>.</summary>
    public string? BoundaryIdentity { get; }
}

/// <summary>
/// Loads and atomically advances event checkpoints across processes and log generations.
/// </summary>
/// <remarks>
/// The primary file remains a dictionary of numeric record IDs. A generation-aware sidecar is authoritative for
/// current clients and prevents a query that started before a log clear from restoring a stale high-water mark.
/// </remarks>
public static class EventCheckpointStore {
    private const int StateVersion = 1;
    private static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Loads a checkpoint snapshot.</summary>
    public static EventCheckpointSnapshot Load(string path) {
        string checkpointPath = NormalizePath(path);
        EnsureParentDirectory(checkpointPath);
        using (AcquireFileLock(checkpointPath, DefaultLockTimeout)) {
            return LoadUnlocked(checkpointPath);
        }
    }

    /// <summary>
    /// Applies generation-checked updates under a cross-process file lock and returns the persisted snapshot.
    /// </summary>
    public static EventCheckpointSnapshot Update(
        string path,
        IEnumerable<EventCheckpointUpdate> updates,
        TimeSpan? lockTimeout = null) {

        if (updates == null) {
            throw new ArgumentNullException(nameof(updates));
        }

        string checkpointPath = NormalizePath(path);
        EnsureParentDirectory(checkpointPath);
        var preparedUpdates =
            new List<EventCheckpointUpdate>();
        foreach (EventCheckpointUpdate? update in updates) {
            if (update == null) {
                throw new ArgumentException(
                    "Checkpoint updates cannot contain null entries.",
                    nameof(updates));
            }
            preparedUpdates.Add(update);
        }

        using (AcquireFileLock(checkpointPath, lockTimeout ?? DefaultLockTimeout)) {
            EventCheckpointSnapshot latest = LoadUnlocked(checkpointPath);
            Dictionary<string, EventCheckpointValue> values = latest.CopyValues();

            foreach (EventCheckpointUpdate update in
                     preparedUpdates) {
                Guid currentGeneration = values.TryGetValue(update.Key, out EventCheckpointValue? current)
                    ? current.GenerationId
                    : Guid.Empty;
                if (currentGeneration != update.ExpectedGenerationId) {
                    continue;
                }

                if (update.StartsNewGeneration) {
                    values[update.Key] = new EventCheckpointValue(update.RecordId, Guid.NewGuid(), update.BoundaryIdentity);
                    continue;
                }

                if (!update.RecordId.HasValue) {
                    continue;
                }

                if (current?.RecordId == null || update.RecordId.Value > current.RecordId.Value) {
                    values[update.Key] = new EventCheckpointValue(update.RecordId, currentGeneration, update.BoundaryIdentity);
                } else if (update.RecordId.Value == current.RecordId.Value &&
                           current.BoundaryIdentity == null && update.BoundaryIdentity != null) {
                    values[update.Key] = new EventCheckpointValue(current.RecordId, currentGeneration, update.BoundaryIdentity);
                }
            }

            WriteState(checkpointPath, values);
            return new EventCheckpointSnapshot(checkpointPath, values);
        }
    }

    /// <summary>
    /// Atomically starts a new checkpoint generation for one key and its derived source keys, or every existing key.
    /// </summary>
    /// <remarks>
    /// Use this method instead of deleting only the compatibility checkpoint file. Generation tombstones are retained
    /// in the authoritative companion state so an in-flight writer from the previous generation cannot restore progress.
    /// </remarks>
    /// <param name="path">Compatibility checkpoint path supplied to event queries.</param>
    /// <param name="key">Optional checkpoint key. The exact key and existing source keys prefixed with <c>key|</c> are reset. Null resets every existing key.</param>
    /// <param name="lockTimeout">Optional cross-process lock timeout.</param>
    /// <returns>The persisted generation-aware checkpoint snapshot.</returns>
    public static EventCheckpointSnapshot Reset(
        string path,
        string? key = null,
        TimeSpan? lockTimeout = null) {

        if (key != null && string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("Checkpoint key cannot be empty when supplied.", nameof(key));
        }

        string checkpointPath = NormalizePath(path);
        EnsureParentDirectory(checkpointPath);
        using (AcquireFileLock(checkpointPath, lockTimeout ?? DefaultLockTimeout)) {
            Dictionary<string, EventCheckpointValue> values = LoadUnlocked(checkpointPath).CopyValues();
            if (key == null) {
                foreach (string existingKey in values.Keys.ToArray()) {
                    values[existingKey] = CreateResetValue();
                }
            } else {
                string normalizedKey = key.Trim();
                string derivedPrefix =
                    normalizedKey + "|";
                string[] derivedKeys = values.Keys
                    .Where(existingKey =>
                        existingKey.StartsWith(
                            derivedPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                values[normalizedKey] = CreateResetValue();
                foreach (string derivedKey in derivedKeys) {
                    values[derivedKey] = CreateResetValue();
                }
            }

            WriteState(checkpointPath, values);
            return new EventCheckpointSnapshot(checkpointPath, values);
        }
    }

    private static EventCheckpointSnapshot LoadUnlocked(string checkpointPath) {
        string statePath = GetStateFilePath(checkpointPath);
        string stateJson;
        try {
            stateJson = File.ReadAllText(statePath);
        } catch (FileNotFoundException) {
            Dictionary<string, EventCheckpointValue> legacyValues = ReadNumericFile(checkpointPath)
                .ToDictionary(
                    static entry => entry.Key,
                    static entry => new EventCheckpointValue(entry.Value, Guid.Empty, boundaryIdentity: null),
                    StringComparer.OrdinalIgnoreCase);
            return new EventCheckpointSnapshot(checkpointPath, legacyValues);
        } catch (DirectoryNotFoundException) {
            Dictionary<string, EventCheckpointValue> legacyValues = ReadNumericFile(checkpointPath)
                .ToDictionary(
                    static entry => entry.Key,
                    static entry => new EventCheckpointValue(entry.Value, Guid.Empty, boundaryIdentity: null),
                    StringComparer.OrdinalIgnoreCase);
            return new EventCheckpointSnapshot(checkpointPath, legacyValues);
        }

        var values = new Dictionary<string, EventCheckpointValue>(StringComparer.OrdinalIgnoreCase);
        CheckpointStateDocument? state;
        try {
            state = JsonSerializer.Deserialize<CheckpointStateDocument>(stateJson);
        } catch (JsonException ex) {
            throw new InvalidDataException($"Checkpoint generation state '{statePath}' is not valid JSON.", ex);
        }

        if (state == null || state.Version != StateVersion || state.Checkpoints == null) {
            throw new InvalidDataException($"Checkpoint generation state '{statePath}' has an unsupported format.");
        }

        foreach (KeyValuePair<string, CheckpointStateEntry> entry in state.Checkpoints) {
            if (entry.Value == null || entry.Value.RecordId < 0 ||
                (!entry.Value.RecordId.HasValue && !string.IsNullOrWhiteSpace(entry.Value.BoundaryIdentity))) {
                throw new InvalidDataException($"Checkpoint generation state '{statePath}' contains an invalid entry for '{entry.Key}'.");
            }
            values[entry.Key] = new EventCheckpointValue(
                entry.Value.RecordId,
                entry.Value.GenerationId,
                entry.Value.BoundaryIdentity);
        }

        return new EventCheckpointSnapshot(checkpointPath, values);
    }

    private static Dictionary<string, long> ReadNumericFile(string checkpointPath) {
        if (!File.Exists(checkpointPath)) {
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, long>? records;
        try {
            records = JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(checkpointPath));
        } catch (JsonException ex) {
            throw new InvalidDataException($"Checkpoint file '{checkpointPath}' is not valid JSON.", ex);
        }

        if (records == null) {
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
        if (records.Any(static entry => entry.Value < 0)) {
            throw new InvalidDataException($"Checkpoint file '{checkpointPath}' contains a negative record ID.");
        }
        return new Dictionary<string, long>(records, StringComparer.OrdinalIgnoreCase);
    }

    private static void WriteState(string checkpointPath, Dictionary<string, EventCheckpointValue> values) {
        var document = new CheckpointStateDocument {
            Version = StateVersion,
            Checkpoints = values.ToDictionary(
                static entry => entry.Key,
                static entry => new CheckpointStateEntry {
                    RecordId = entry.Value.RecordId,
                    GenerationId = entry.Value.GenerationId,
                    BoundaryIdentity = entry.Value.BoundaryIdentity
                },
                StringComparer.OrdinalIgnoreCase)
        };
        var numeric = values
            .Where(static entry => entry.Value.RecordId.HasValue)
            .ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value.RecordId!.Value,
                StringComparer.OrdinalIgnoreCase);

        AtomicWrite(GetStateFilePath(checkpointPath), JsonSerializer.Serialize(document));
        try {
            AtomicWrite(checkpointPath, JsonSerializer.Serialize(numeric));
        } catch (IOException ex) {
            Settings._logger.WriteWarning(
                $"The authoritative checkpoint state was saved, but the legacy numeric mirror '{checkpointPath}' could not be updated: {ex.Message}");
        } catch (UnauthorizedAccessException ex) {
            Settings._logger.WriteWarning(
                $"The authoritative checkpoint state was saved, but the legacy numeric mirror '{checkpointPath}' could not be updated: {ex.Message}");
        }
    }

    private static FileStream AcquireFileLock(string checkpointPath, TimeSpan timeout) {
        if (timeout < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Lock timeout must be greater than or equal to zero.");
        }

        string lockPath = GetLockFilePath(checkpointPath);
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (true) {
            try {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } catch (IOException ex) {
                if (DateTime.UtcNow >= deadline) {
                    throw new TimeoutException($"Timed out waiting to update shared event checkpoint file '{checkpointPath}'.", ex);
                }
                Thread.Sleep(50);
            }
        }
    }

    internal static void AtomicWrite(
        string path,
        string contents,
        Action<string>? deleteTemporary = null) {

        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(temporaryPath, contents);
            if (File.Exists(path)) {
                File.Replace(temporaryPath, path, null);
            } else {
                File.Move(temporaryPath, path);
            }
        } finally {
            DeleteTemporaryBestEffort(
                temporaryPath,
                deleteTemporary);
        }
    }

    private static void DeleteTemporaryBestEffort(
        string temporaryPath,
        Action<string>? deleteTemporary) {

        try {
            if (File.Exists(temporaryPath)) {
                (deleteTemporary ?? File.Delete)(
                    temporaryPath);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private static string NormalizePath(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Checkpoint path cannot be null or empty.", nameof(path));
        }
        return Path.GetFullPath(path);
    }

    private static void EnsureParentDirectory(string checkpointPath) {
        string? directory = Path.GetDirectoryName(checkpointPath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>Returns the authoritative companion state path for a compatibility checkpoint path.</summary>
    public static string GetStateFilePath(string path)
        => NormalizePath(path) + ".state.json";

    /// <summary>Returns the cross-process lock path for a compatibility checkpoint path.</summary>
    public static string GetLockFilePath(string path)
        => NormalizePath(path) + ".lock";

    private static EventCheckpointValue CreateResetValue()
        => new(recordId: null, Guid.NewGuid(), boundaryIdentity: null);

    private sealed class CheckpointStateDocument {
        public int Version { get; set; }
        public Dictionary<string, CheckpointStateEntry>? Checkpoints { get; set; }
    }

    private sealed class CheckpointStateEntry {
        public long? RecordId { get; set; }
        public Guid GenerationId { get; set; }
        public string? BoundaryIdentity { get; set; }
    }
}
