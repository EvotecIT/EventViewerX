using EventViewerX;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventQueryPlanner {
    [Fact]
    public void RequiresExactlyOneSourceFamily() {
        var definition = new EventQueryDefinition {
            LogNames = new[] { "System" },
            Paths = new[] { "events.evtx" }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EventQueryPlanner.CreateBatch(definition));

        Assert.Contains("Exactly one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsProviderNamesCombinedWithOpaqueXPath() {
        var definition = new EventQueryDefinition {
            ProviderNames = new[] { "Microsoft-Windows-*" },
            FilterXPath = "*[System[Level=2]]"
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EventQueryPlanner.CreateBatch(definition));

        Assert.Contains("ProviderNames and FilterXPath", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildsOfflineFileBatchWithSharedOptions() {
        string path = Path.GetTempFileName();
        try {
            EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
                new EventQueryDefinition {
                    Paths = new[] { path },
                    Filter = new EventFilter {
                        EventIds = new[] { 1000, 1001 }
                    },
                    Options = new EventLogQueryOptions {
                        Oldest = true,
                        ReadMode = EventReadMode.Metadata,
                        MaxEvents = 25,
                        MaxConcurrency = 3
                    }
                });

            EventLogStructuredQuery query = Assert.Single(batch.StructuredQueries);
            Assert.True(query.Oldest);
            Assert.Equal(EventReadMode.Metadata, query.ReadMode);
            Assert.Equal(25, batch.MaxEvents);
            Assert.Equal(3, batch.MaxConcurrency);
            Assert.Equal(EventLogQuerySourceKind.File, query.SourceKind);
            Assert.Contains("EventID=1000", query.QueryXml, StringComparison.Ordinal);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExpandsDirectoryToEvtxFilesOnly() {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-QueryPlanner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            string first = Path.Combine(directory, "one.evtx");
            string second = Path.Combine(directory, "two.evtx");
            File.WriteAllText(first, string.Empty);
            File.WriteAllText(second, string.Empty);
            File.WriteAllText(Path.Combine(directory, "ignore.txt"), string.Empty);

            EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
                new EventQueryDefinition {
                    Paths = new[] { directory }
                });

            Assert.Equal(2, batch.StructuredQueries.Count);
            Assert.All(
                batch.StructuredQueries,
                query => Assert.Equal(EventLogQuerySourceKind.File, query.SourceKind));
            Assert.Contains(batch.StructuredQueries, query =>
                query.QueryXml.Contains("one.evtx", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(batch.StructuredQueries, query =>
                query.QueryXml.Contains("two.evtx", StringComparison.OrdinalIgnoreCase));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExpandsFileNameWildcardAfterCanonicalizingItsDirectory() {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-QueryPlanner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            File.WriteAllText(Path.Combine(directory, "one.evtx"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "two.evtx"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "ignore.txt"), string.Empty);

            EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
                new EventQueryDefinition {
                    Paths = new[] { Path.Combine(directory, "*.evtx") }
                });

            Assert.Equal(2, batch.StructuredQueries.Count);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExpandsWildcardDirectorySegmentsBeforeCanonicalizingMatches() {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-QueryPlanner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            string first = Directory.CreateDirectory(
                Path.Combine(directory, "Archive-One")).FullName;
            string second = Directory.CreateDirectory(
                Path.Combine(directory, "Archive-Two")).FullName;
            string ignored = Directory.CreateDirectory(
                Path.Combine(directory, "Current")).FullName;
            File.WriteAllText(Path.Combine(first, "one.evtx"), string.Empty);
            File.WriteAllText(Path.Combine(second, "two.evtx"), string.Empty);
            File.WriteAllText(Path.Combine(ignored, "ignored.evtx"), string.Empty);

            EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
                new EventQueryDefinition {
                    Paths = new[] { Path.Combine(directory, "Archive-*", "*.evtx") }
                });

            Assert.Equal(2, batch.StructuredQueries.Count);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsWildcardUncShareBeforeCanonicalizingItsRoot() {
        var exception = Assert.Throws<ArgumentException>(() =>
            EventQueryPlanner.CreateBatch(
                new EventQueryDefinition {
                    Paths = new[] { @"\\server\Archive-*\*.evtx" }
                }));

        Assert.Contains("UNC share name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsWindowsExtendedLengthFilePathWithoutTreatingPrefixAsWildcard() {
        string path = Path.GetTempFileName();
        try {
            string extendedPath = @"\\?\" + Path.GetFullPath(path);

            EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
                new EventQueryDefinition {
                    Paths = new[] { extendedPath }
                });

            EventLogStructuredQuery query = Assert.Single(batch.StructuredQueries);
            Assert.Equal(EventLogQuerySourceKind.File, query.SourceKind);
            Assert.False(FileSystemPathIdentity.ContainsWildcard(extendedPath));
            EventLogStructuredQuerySource source = Assert.Single(query.ResolveSources());
            Assert.Equal(Path.GetFullPath(path), source.Source);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExpandsWildcardAfterWindowsExtendedLengthPrefix() {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-ExtendedPaths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            File.WriteAllText(Path.Combine(directory, "one.evtx"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "two.evtx"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "ignore.txt"), string.Empty);
            string extendedPattern = Path.Combine(
                @"\\?\" + Path.GetFullPath(directory),
                "*.evtx");

            EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
                new EventQueryDefinition {
                    Paths = new[] { extendedPattern }
                });

            Assert.Equal(2, batch.StructuredQueries.Count);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(@"\\?\C:\logs\*.evtx")]
    [InlineData(@"\\?\UNC\server\share\log?.evtx")]
    public void DetectsWildcardsAfterWindowsExtendedLengthPrefix(string path) {
        Assert.True(FileSystemPathIdentity.ContainsWildcard(path));
    }

    [Theory]
    [InlineData(@"\\?\C:\logs\archive.evtx")]
    [InlineData(@"\\?\UNC\server\share\archive.evtx")]
    public void DoesNotTreatWindowsExtendedLengthPrefixAsWildcard(string path) {
        Assert.False(FileSystemPathIdentity.ContainsWildcard(path));
    }

    [Fact]
    public void NormalizesWindowsExtendedLengthDriveAndUncPaths() {
        Assert.Equal(
            @"C:\logs\archive.evtx",
            FileSystemPathIdentity.NormalizeWindowsExtendedLengthPath(
                @"\\?\C:\logs\archive.evtx"));
        Assert.Equal(
            @"\\server\share\archive.evtx",
            FileSystemPathIdentity.NormalizeWindowsExtendedLengthPath(
                @"\\?\UNC\server\share\archive.evtx"));
    }

    [Fact]
    public void ReadsAnEventFileSuppliedWithAnExtendedLengthPath() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "Tests", "Logs", "NamedFilterExamples.evtx"));
        string extendedPath = @"\\?\" + fixture;

        EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
            new EventQueryDefinition {
                Paths = new[] { extendedPath },
                Options = new EventLogQueryOptions {
                    MaxEvents = 1,
                    ReadMode = EventReadMode.Metadata
                }
            });
        EventObject item = Assert.Single(EventLogBatchEngine.Read(batch));

        Assert.Equal(
            fixture,
            item.GatheredFrom,
            ignoreCase: true,
            ignoreLineEndingDifferences: false,
            ignoreWhiteSpaceDifferences: false);
    }

    [Fact]
    public void RejectsBookmarkFanOut() {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-QueryPlanner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            File.WriteAllText(Path.Combine(directory, "one.evtx"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "two.evtx"), string.Empty);

            var definition = new EventQueryDefinition {
                Paths = new[] { directory },
                Options = new EventLogQueryOptions {
                    BookmarkXml = "<BookmarkList />"
                }
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => EventQueryPlanner.CreateBatch(definition));
            Assert.Contains("exactly one native query source", exception.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsRemoteMachineForFileQueryXml() {
        var definition = new EventQueryDefinition {
            QueryXml =
                "<QueryList><Query Id=\"0\" Path=\"file:///C:/events.evtx\"><Select Path=\"file:///C:/events.evtx\">*</Select></Query></QueryList>",
            MachineNames = new[] { "remote.contoso.test" }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EventQueryPlanner.CreateBatch(definition));

        Assert.Contains(
            "cannot be combined with MachineNames",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AllowsImplicitLocalNullTargetForFileQueryXml() {
        EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
            new EventQueryDefinition {
                QueryXml =
                    "<QueryList><Query Id=\"0\" Path=\"file:///C:/events.evtx\"><Select Path=\"file:///C:/events.evtx\">*</Select></Query></QueryList>",
                MachineNames = new string?[] { null }
            });

        EventLogStructuredQuery query =
            Assert.Single(batch.StructuredQueries);
        Assert.Null(query.MachineName);
        Assert.Equal(EventLogQuerySourceKind.File, query.SourceKind);
    }

    [Fact]
    public void RejectsRemoteMachineForMixedChannelAndFileQueryXmlEvenWhenContinuingOnError() {
        var definition = new EventQueryDefinition {
            QueryXml =
                "<QueryList>" +
                "<Query Id=\"0\" Path=\"System\"><Select Path=\"System\">*</Select></Query>" +
                "<Query Id=\"1\" Path=\"file:///C:/events.evtx\"><Select Path=\"file:///C:/events.evtx\">*</Select></Query>" +
                "</QueryList>",
            MachineNames = new[] { "remote.contoso.test" },
            Options = new EventLogQueryOptions {
                ContinueOnError = true
            }
        };

        Assert.Throws<ArgumentException>(
            () => EventQueryPlanner.CreateBatch(definition));
    }

    [Fact]
    public void RejectsCredentialForFileQueryXml() {
        var definition = new EventQueryDefinition {
            QueryXml =
                "<QueryList><Query Id=\"0\" Path=\"file:///C:/events.evtx\"><Select Path=\"file:///C:/events.evtx\">*</Select></Query></QueryList>",
            Options = new EventLogQueryOptions {
                Credential = new System.Net.NetworkCredential(
                    "reader",
                    "password")
            }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EventQueryPlanner.CreateBatch(definition));

        Assert.Contains(
            "every query target is a remote computer",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AllowsCredentialForRemoteChannelQueryXml() {
        var credential = new System.Net.NetworkCredential(
            "reader",
            "password");
        EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
            new EventQueryDefinition {
                QueryXml =
                    "<QueryList><Query Id=\"0\" Path=\"System\"><Select Path=\"System\">*</Select></Query></QueryList>",
                MachineNames = new[] { "remote.contoso.test" },
                Options = new EventLogQueryOptions {
                    Credential = credential
                }
            });

        EventLogStructuredQuery query =
            Assert.Single(batch.StructuredQueries);
        Assert.Equal("remote.contoso.test", query.MachineName);
        Assert.Equal("reader", query.Credential!.UserName);
    }
}
