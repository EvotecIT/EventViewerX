using System;
using System.IO;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventCheckpointStore {
    [Fact]
    public void LegacyNumericCheckpointIsLoadedAndAdvancedInANestedDirectory() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "nested", "checkpoint.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{\"System\":100}");

            EventCheckpointSnapshot initial = EventCheckpointStore.Load(path);
            Assert.Equal(100, initial.Records["System"]);
            Assert.True(initial.TryGetValue("System", out EventCheckpointValue? value));
            Assert.Equal(Guid.Empty, value!.GenerationId);

            EventCheckpointSnapshot updated = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 101, value.GenerationId, boundaryIdentity: "boundary-101") });

            Assert.Equal(101, updated.Records["System"]);
            Assert.Equal("boundary-101", updated.Checkpoints["System"].BoundaryIdentity);
            Assert.True(File.Exists(path + ".state.json"));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BoundaryIdentityCanBeMigratedWithoutAdvancingTheRecord() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            EventCheckpointSnapshot initial = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 100, Guid.Empty) });

            EventCheckpointSnapshot migrated = EventCheckpointStore.Update(
                path,
                new[] {
                    new EventCheckpointUpdate(
                        "System",
                        100,
                        initial.Checkpoints["System"].GenerationId,
                        boundaryIdentity: "boundary-100")
                });

            Assert.Equal(100, migrated.Records["System"]);
            Assert.Equal("boundary-100", migrated.Checkpoints["System"].BoundaryIdentity);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResetGenerationRejectsAWriterThatStartedBeforeTheReset() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            EventCheckpointSnapshot initial = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 1000, Guid.Empty) });
            Guid oldGeneration = initial.Checkpoints["System"].GenerationId;

            EventCheckpointSnapshot reset = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 5, oldGeneration, startsNewGeneration: true) });
            Guid newGeneration = reset.Checkpoints["System"].GenerationId;
            Assert.NotEqual(oldGeneration, newGeneration);

            EventCheckpointSnapshot grownGeneration = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 1005, newGeneration) });
            Assert.Equal(1005, grownGeneration.Records["System"]);

            EventCheckpointSnapshot staleResult = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 1010, oldGeneration) });
            Assert.Equal(1005, staleResult.Records["System"]);
            Assert.Equal(newGeneration, staleResult.Checkpoints["System"].GenerationId);

            EventCheckpointSnapshot currentResult = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 1006, newGeneration) });
            Assert.Equal(1006, currentResult.Records["System"]);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResetWithoutRecordsPersistsATombstoneThatRejectsStaleProgress() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            EventCheckpointSnapshot initial = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 1000, Guid.Empty) });
            Guid oldGeneration = initial.Checkpoints["System"].GenerationId;

            EventCheckpointSnapshot reset = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", null, oldGeneration, startsNewGeneration: true) });
            Assert.DoesNotContain("System", reset.Records.Keys);
            Assert.NotEqual(oldGeneration, reset.Checkpoints["System"].GenerationId);

            EventCheckpointSnapshot staleResult = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 1001, oldGeneration) });
            Assert.DoesNotContain("System", staleResult.Records.Keys);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CorruptCheckpointFailsInsteadOfSilentlyDiscardingProgress() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            File.WriteAllText(path, "not-json");

            Assert.Throws<InvalidDataException>(() => EventCheckpointStore.Load(path));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AuthoritativeStateAccessFailureDoesNotFallBackToLegacy() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(
                root,
                "checkpoint.json");
            File.WriteAllText(
                path,
                "{\"System\":100}");
            Directory.CreateDirectory(
                path + ".state.json");

            Assert.Throws<UnauthorizedAccessException>(
                () => EventCheckpointStore.Load(
                    path));
        } finally {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void NumericCompatibilityMirrorFailureDoesNotInvalidateAuthoritativeUpdate() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            EventCheckpointSnapshot initial = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 100, Guid.Empty) });

            File.Delete(path);
            Directory.CreateDirectory(path);

            EventCheckpointSnapshot updated = EventCheckpointStore.Update(
                path,
                new[] {
                    new EventCheckpointUpdate(
                        "System",
                        101,
                        initial.Checkpoints["System"].GenerationId)
                });

            Assert.Equal(101, updated.Records["System"]);
            Assert.Equal(101, EventCheckpointStore.Load(path).Records["System"]);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AuthoritativeStateLoadsWhenTheCompatibilityMirrorIsCorrupt() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            EventCheckpointSnapshot initial = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 100, Guid.Empty) });
            File.WriteAllText(path, "not-json");

            EventCheckpointSnapshot loaded = EventCheckpointStore.Load(path);
            EventCheckpointSnapshot updated = EventCheckpointStore.Update(
                path,
                new[] {
                    new EventCheckpointUpdate(
                        "System",
                        101,
                        initial.Checkpoints["System"].GenerationId)
                });

            Assert.Equal(100, loaded.Records["System"]);
            Assert.Equal(101, updated.Records["System"]);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResetStartsANewGenerationAndRejectsAnInFlightWriter() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            EventCheckpointSnapshot initial = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 100, Guid.Empty, boundaryIdentity: "boundary-100") });
            Guid oldGeneration = initial.Checkpoints["System"].GenerationId;

            EventCheckpointSnapshot reset = EventCheckpointStore.Reset(path, "System");

            Assert.DoesNotContain("System", reset.Records.Keys);
            Assert.NotEqual(oldGeneration, reset.Checkpoints["System"].GenerationId);
            Assert.Equal(Path.GetFullPath(path), reset.CheckpointPath);
            Assert.Equal(Path.GetFullPath(path) + ".state.json", reset.StatePath);
            Assert.Equal(Path.GetFullPath(path) + ".lock", reset.LockPath);

            EventCheckpointSnapshot staleResult = EventCheckpointStore.Update(
                path,
                new[] { new EventCheckpointUpdate("System", 101, oldGeneration) });
            Assert.DoesNotContain("System", staleResult.Records.Keys);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResetKeyAlsoResetsItsDerivedSourceCheckpoints() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            EventCheckpointSnapshot initial = EventCheckpointStore.Update(
                path,
                new[] {
                    new EventCheckpointUpdate("CriticalEvents|AD1|System", 100, Guid.Empty),
                    new EventCheckpointUpdate("CriticalEvents|AD2|System", 200, Guid.Empty),
                    new EventCheckpointUpdate("CriticalEvents2|AD1|System", 300, Guid.Empty)
                });
            Guid firstGeneration =
                initial.Checkpoints["CriticalEvents|AD1|System"]
                    .GenerationId;
            Guid secondGeneration =
                initial.Checkpoints["CriticalEvents|AD2|System"]
                    .GenerationId;

            EventCheckpointSnapshot reset =
                EventCheckpointStore.Reset(
                    path,
                    "CriticalEvents");

            Assert.Null(
                reset.Checkpoints["CriticalEvents"].RecordId);
            Assert.Null(
                reset.Checkpoints["CriticalEvents|AD1|System"]
                    .RecordId);
            Assert.Null(
                reset.Checkpoints["CriticalEvents|AD2|System"]
                    .RecordId);
            Assert.NotEqual(
                firstGeneration,
                reset.Checkpoints["CriticalEvents|AD1|System"]
                    .GenerationId);
            Assert.NotEqual(
                secondGeneration,
                reset.Checkpoints["CriticalEvents|AD2|System"]
                    .GenerationId);
            Assert.Equal(
                300,
                reset.Records["CriticalEvents2|AD1|System"]);

            EventCheckpointSnapshot staleResult =
                EventCheckpointStore.Update(
                    path,
                    new[] {
                        new EventCheckpointUpdate(
                            "CriticalEvents|AD1|System",
                            101,
                            firstGeneration)
                    });

            Assert.DoesNotContain(
                "CriticalEvents|AD1|System",
                staleResult.Records.Keys);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResetAllPreservesOtherKeysAsGenerationTombstones() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(root, "checkpoint.json");
            EventCheckpointSnapshot initial = EventCheckpointStore.Update(
                path,
                new[] {
                    new EventCheckpointUpdate("System", 100, Guid.Empty),
                    new EventCheckpointUpdate("Application", 200, Guid.Empty)
                });

            EventCheckpointSnapshot reset = EventCheckpointStore.Reset(path);

            Assert.Empty(reset.Records);
            Assert.Equal(2, reset.Checkpoints.Count);
            Assert.All(reset.Checkpoints.Values, static value => Assert.Null(value.RecordId));
            Assert.NotEqual(initial.Checkpoints["System"].GenerationId, reset.Checkpoints["System"].GenerationId);
            Assert.NotEqual(initial.Checkpoints["Application"].GenerationId, reset.Checkpoints["Application"].GenerationId);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LazyUpdatesAreMaterializedBeforeTheCheckpointLock() {
        string root = CreateTemporaryDirectory();
        try {
            string path = Path.Combine(
                root,
                "checkpoint.json");

            EventCheckpointSnapshot updated =
                EventCheckpointStore.Update(
                    path,
                    CreateReentrantUpdates(),
                    TimeSpan.FromSeconds(1));

            Assert.Equal(
                1,
                updated.Records["System"]);

            IEnumerable<EventCheckpointUpdate>
                CreateReentrantUpdates() {

                EventCheckpointStore.Update(
                    path,
                    Array.Empty<EventCheckpointUpdate>(),
                    TimeSpan.Zero);
                yield return new EventCheckpointUpdate(
                    "System",
                    1,
                    Guid.Empty);
            }
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AtomicWriteCleanupCannotReplaceThePrimaryFailure() {
        string root = CreateTemporaryDirectory();
        string destination = Path.Combine(
            root,
            "existing-directory");
        Directory.CreateDirectory(destination);
        try {
            IOException exception =
                Assert.Throws<IOException>(() =>
                    EventCheckpointStore.AtomicWrite(
                        destination,
                        "{}",
                        _ => throw new IOException(
                            "simulated cleanup failure")));

            Assert.DoesNotContain(
                "simulated cleanup failure",
                exception.Message,
                StringComparison.Ordinal);
        } finally {
            foreach (string temporary in
                     Directory.EnumerateFiles(
                         root,
                         "existing-directory.*.tmp")) {
                File.Delete(temporary);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory() {
        string path = Path.Combine(Path.GetTempPath(), "EventViewerX.Tests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
