using System.Xml.Linq;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventFilterCompiler {
    [Theory]
    [InlineData(100L, null, 100L)]
    [InlineData(100L, 50L, 100L)]
    [InlineData(100L, 100L, 100L)]
    [InlineData(100L, 150L, 150L)]
    public void MinimumRecordBoundaryCompositionKeepsTheStricterValue(
        long existing,
        long? checkpoint,
        long expected) {

        var source = new EventFilter {
            MinimumRecordIdExclusive = existing
        };

        EventFilter combined =
            source.WithMinimumRecordIdExclusive(checkpoint);

        Assert.Equal(expected, combined.MinimumRecordIdExclusive);
        Assert.Equal(existing, source.MinimumRecordIdExclusive);
        Assert.NotSame(source, combined);
    }

    [Fact]
    public void TypedFilterBuildsTheExpectedNativeDimensions() {
        var filter = new EventFilter {
            EventIds = new[] { 4624, 4625 },
            RecordIds = new long[] { 10, 11 },
            ProviderNames = new[] {
                "Microsoft-Windows-Security-Auditing",
                "Custom Provider"
            },
            Levels = new byte[] { 2, 3 },
            Keywords = new long[] { 1, 2 },
            StartTime = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            UserIds = new[] { "S-1-5-18" },
            Data = new[] { "payload" },
            NamedData = new Dictionary<string, IReadOnlyList<string>> {
                ["TargetUserName"] = new[] { "alice", "bob" }
            },
            ExcludedEventIds = new[] { 4634 }
        };

        string xpath = EventFilterCompiler.BuildXPath(filter);

        Assert.Contains("EventID=4624", xpath, StringComparison.Ordinal);
        Assert.Contains("EventID=4625", xpath, StringComparison.Ordinal);
        Assert.Contains("EventID!=4634", xpath, StringComparison.Ordinal);
        Assert.Contains("EventRecordID=10", xpath, StringComparison.Ordinal);
        Assert.Contains("Microsoft-Windows-Security-Auditing", xpath, StringComparison.Ordinal);
        Assert.Contains("Level=2", xpath, StringComparison.Ordinal);
        Assert.Contains("band(Keywords,3)", xpath, StringComparison.Ordinal);
        Assert.Contains("S-1-5-18", xpath, StringComparison.Ordinal);
        Assert.Contains("TargetUserName", xpath, StringComparison.Ordinal);
        Assert.Contains("alice", xpath, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedFilterSupportsCustomProviderLevelsAndEventIdZero() {
        string xpath = EventFilterCompiler.BuildXPath(
            new EventFilter {
                EventIds = new[] { 0 },
                ExcludedEventIds = new[] { 65535 },
                Levels = new byte[] { 16, 255 }
            });

        Assert.Contains("EventID=0", xpath, StringComparison.Ordinal);
        Assert.Contains("EventID!=65535", xpath, StringComparison.Ordinal);
        Assert.Contains("Level=16", xpath, StringComparison.Ordinal);
        Assert.Contains("Level=255", xpath, StringComparison.Ordinal);
    }

    [Fact]
    public void UnnamedDataPreservesEmptyWhitespaceAndCaseDistinctLiterals() {
        var filter = new EventFilter {
            Data = new[] {
                string.Empty,
                " Ready ",
                "Ready",
                "ready"
            }
        };

        string xpath =
            EventFilterCompiler.BuildXPath(
                filter);

        Assert.Contains(
            "Data=''",
            xpath,
            StringComparison.Ordinal);
        Assert.Contains(
            "Data=' Ready '",
            xpath,
            StringComparison.Ordinal);
        Assert.Contains(
            "Data='Ready'",
            xpath,
            StringComparison.Ordinal);
        Assert.Contains(
            "Data='ready'",
            xpath,
            StringComparison.Ordinal);
        Assert.Equal(
            4,
            EventFilterCompiler.CountExpressions(
                filter));
    }

    [Fact]
    public void StructuredQuerySupportsSeveralChannelsAndSuppressions() {
        string queryXml = EventFilterCompiler.BuildChannelQueryXml(
            new[] { "System", "Application", "System" },
            new EventFilter {
                Levels = new byte[] { 2, 3 }
            },
            new EventFilter {
                ProviderNames = new[] { "Noisy Provider" }
            });

        XDocument document = XDocument.Parse(queryXml);
        XElement[] queries = document.Root!.Elements("Query").ToArray();
        Assert.Equal(2, queries.Length);
        Assert.Equal(
            new[] { "System", "Application" },
            queries.Select(static query => query.Attribute("Path")!.Value));
        Assert.All(queries, static query => {
            Assert.Contains("Level=2", query.Element("Select")!.Value, StringComparison.Ordinal);
            Assert.Contains("Noisy Provider", query.Element("Suppress")!.Value, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ExcludedNamedDataBecomesAStructuredSuppression() {
        string queryXml = EventFilterCompiler.BuildChannelQueryXml(
            new[] { "System" },
            new EventFilter {
                EventIds = new[] { 7040 },
                ExcludedNamedData =
                    new Dictionary<string, IReadOnlyList<string>> {
                        ["param4"] = new[] { "BITS" }
                    }
            });

        XElement query = XDocument.Parse(queryXml)
            .Root!
            .Element("Query")!;
        Assert.Contains(
            "EventID=7040",
            query.Element("Select")!.Value,
            StringComparison.Ordinal);
        Assert.Equal(
            "*[EventData[Data[@Name='param4'] = 'BITS']]",
            query.Element("Suppress")!.Value);
    }

    [Fact]
    public void RawXpathRejectsExcludedNamedData() {
        var filter = new EventFilter {
            ExcludedNamedData =
                new Dictionary<string, IReadOnlyList<string>> {
                    ["FieldName"] = new[] { "Value1" }
                }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            EventFilterCompiler.BuildXPath(filter));

        Assert.Contains("QueryList", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredQueryAcceptsPartitionedSuppressionsBeyondNativeLimit() {
        var suppress = new EventFilter {
            EventIds = Enumerable.Range(1, 60).ToArray()
        };
        IReadOnlyList<EventFilter> partitions =
            EventFilterPartitioner.Partition(suppress);

        string queryXml =
            EventFilterCompiler.BuildChannelQueryXmlWithSuppressions(
            new[] { "System" },
            select: null,
            partitions);

        XElement query = XDocument.Parse(queryXml)
            .Root!
            .Element("Query")!;
        XElement[] suppressions = query
            .Elements("Suppress")
            .ToArray();
        Assert.True(suppressions.Length > 1);
        Assert.All(suppressions, static item =>
            Assert.InRange(
                item.Value.Split(new[] { "EventID=" }, StringSplitOptions.None).Length - 1,
                1,
                EventFilterCompiler.MaximumXPathExpressions));
        Assert.Equal(
            60,
            suppressions.Sum(static item =>
                item.Value.Split(new[] { "EventID=" }, StringSplitOptions.None).Length - 1));
    }

    [Fact]
    public void FilterRejectsAnInvertedTimeRange() {
        var filter = new EventFilter {
            StartTime = new DateTime(2026, 7, 2),
            EndTime = new DateTime(2026, 7, 1)
        };

        Assert.Throws<ArgumentException>(() =>
            EventFilterCompiler.BuildXPath(filter));
    }

    [Fact]
    public void ExpressionCountMatchesProviderAndNamedDataCost() {
        var filter = new EventFilter {
            EventIds = new[] { 1, 2 },
            ProviderNames = new[] { "One", "Two", "Three" },
            Keywords = new long[] { 1, 2 },
            NamedData =
                new Dictionary<string, IReadOnlyList<string>> {
                    ["User"] = new[] { "alice", "bob" }
                }
        };

        Assert.Equal(
            10,
            EventFilterCompiler.CountExpressions(filter));
        Assert.Equal(
            22,
            EventFilterCompiler.MaximumXPathExpressions);
    }

    [Fact]
    public void PartitionerPreservesCartesianAndSemanticsWithinNativeLimit() {
        var filter = new EventFilter {
            EventIds = Enumerable.Range(1, 40).ToArray(),
            ProviderNames = Enumerable.Range(1, 30)
                .Select(static value => $"Provider-{value}")
                .ToArray(),
            StartTime = new DateTime(
                2026,
                7,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc)
        };

        IReadOnlyList<EventFilter> partitions =
            EventFilterPartitioner.Partition(filter);

        Assert.True(partitions.Count > 1);
        Assert.All(partitions, static partition =>
            Assert.InRange(
                EventFilterCompiler.CountExpressions(partition),
                1,
                EventFilterCompiler.MaximumXPathExpressions));
        HashSet<(int EventId, string Provider)> combinations =
            partitions
                .SelectMany(partition =>
                    partition.EventIds!.SelectMany(
                        eventId =>
                            partition.ProviderNames!.Select(
                                provider =>
                                    (eventId, provider))))
                .ToHashSet();
        Assert.Equal(40 * 30, combinations.Count);
    }

    [Fact]
    public void ChannelUnionQueryUsesOneQueryWithSeveralSelectClauses() {
        string xml =
            EventFilterCompiler
                .BuildChannelUnionQueryXml(
                    new[] { "Application" },
                    new[] {
                        new EventFilter {
                            EventIds =
                                new[] { 1 }
                        },
                        new EventFilter {
                            EventIds =
                                new[] { 2 }
                        }
                    });
        var document =
            System.Xml.Linq.XDocument.Parse(
                xml);

        Assert.Single(
            document.Descendants("Query"));
        Assert.Equal(
            2,
            document.Descendants("Select")
                .Count());
    }

    [Fact]
    public void ChannelUnionScopesNamedDataExclusionsToTheirSelect() {
        string xml =
            EventFilterCompiler
                .BuildChannelUnionQueryXml(
                    new[] { "Application" },
                    new[] {
                        new EventFilter {
                            EventIds =
                                new[] { 1 },
                            ExcludedNamedData =
                                new Dictionary<
                                    string,
                                    IReadOnlyList<string>> {
                                    ["State"] =
                                        new[] { "Ignored" }
                                }
                        },
                        new EventFilter {
                            EventIds =
                                new[] { 2 }
                        }
                    });
        XElement query = XDocument.Parse(xml)
            .Root!
            .Element("Query")!;
        XElement suppression =
            Assert.Single(
                query.Elements("Suppress"));

        Assert.Contains(
            "EventID=1",
            suppression.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "EventID=2",
            suppression.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "State",
            suppression.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "Ignored",
            suppression.Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ChannelUnionOmitsImpossibleSameFieldSuppression() {
        string xml =
            EventFilterCompiler
                .BuildChannelUnionQueryXml(
                    new[] { "Application" },
                    new[] {
                        new EventFilter {
                            EventIds =
                                new[] { 1 },
                            NamedData =
                                new Dictionary<
                                    string,
                                    IReadOnlyList<string>> {
                                    ["State"] =
                                        new[] { "Included" }
                                },
                            ExcludedNamedData =
                                new Dictionary<
                                    string,
                                    IReadOnlyList<string>> {
                                    ["State"] =
                                        new[] { "Ignored" }
                                }
                        }
                    });
        XElement query = XDocument.Parse(xml)
            .Root!
            .Element("Query")!;

        Assert.Empty(
            query.Elements("Suppress"));
    }

    [Fact]
    public void ChannelUnionUsesCaseSensitiveNamedDataIntersections() {
        string xml =
            EventFilterCompiler
                .BuildChannelUnionQueryXml(
                    new[] { "Application" },
                    new[] {
                        new EventFilter {
                            NamedData =
                                new Dictionary<
                                    string,
                                    IReadOnlyList<string>> {
                                    ["State"] =
                                        new[] { "Ready" }
                                },
                            ExcludedNamedData =
                                new Dictionary<
                                    string,
                                    IReadOnlyList<string>> {
                                    ["State"] =
                                        new[] { "ready" }
                                }
                        }
                    });
        XElement query = XDocument.Parse(xml)
            .Root!
            .Element("Query")!;

        Assert.Empty(
            query.Elements("Suppress"));
    }

    [Fact]
    public void ChannelUnionRetainsExclusionsForExistenceOnlyNamedData() {
        string xml =
            EventFilterCompiler
                .BuildChannelUnionQueryXml(
                    new[] { "Application" },
                    new[] {
                        new EventFilter {
                            NamedData =
                                new Dictionary<
                                    string,
                                    IReadOnlyList<string>> {
                                    ["State"] =
                                        Array.Empty<string>()
                                },
                            ExcludedNamedData =
                                new Dictionary<
                                    string,
                                    IReadOnlyList<string>> {
                                    ["State"] =
                                        new[] { "Ignored" }
                                }
                        }
                    });
        XElement query = XDocument.Parse(xml)
            .Root!
            .Element("Query")!;
        XElement suppression =
            Assert.Single(
                query.Elements("Suppress"));

        Assert.Contains(
            "State",
            suppression.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "Ignored",
            suppression.Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FileQueryEscapesReservedUriCharacters() {
        string path = Path.GetFullPath(
            Path.Combine(
                "logs",
                "compiler#source?.evtx"));
        string xml =
            EventFilterCompiler.BuildFileQueryXml(
                new[] { path });
        EventLogStructuredQuerySource source =
            Assert.Single(
                new EventLogStructuredQuery(xml)
                    .ResolveSources());

        Assert.Equal(path, source.Source);
        Assert.Contains(
            "%23",
            xml,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "%3F",
            xml,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PartitionerPreservesNamedDataAcrossOtherDimensions() {
        var filter = new EventFilter {
            EventIds = Enumerable.Range(1, 30).ToArray(),
            NamedData =
                new Dictionary<string, IReadOnlyList<string>> {
                    ["Field"] = Enumerable.Range(1, 12)
                        .Select(static value => $"Value-{value}")
                        .ToArray()
                }
        };

        IReadOnlyList<EventFilter> partitions =
            EventFilterPartitioner.Partition(filter);

        Assert.All(partitions, static partition =>
            Assert.InRange(
                EventFilterCompiler.CountExpressions(partition),
                1,
                EventFilterCompiler.MaximumXPathExpressions));
        HashSet<(int EventId, string Value)> combinations =
            partitions
                .SelectMany(partition =>
                    partition.EventIds!.SelectMany(
                        eventId =>
                            partition.NamedData!["Field"].Select(
                                value =>
                                    (eventId, value))))
                .ToHashSet();
        Assert.Equal(30 * 12, combinations.Count);
    }

    [Fact]
    public void PartitionerPreservesAndAcrossNamedDataKeys() {
        var filter = new EventFilter {
            NamedData =
                new Dictionary<string, IReadOnlyList<string>> {
                    ["First"] = Enumerable.Range(1, 12)
                        .Select(static value => $"A-{value}")
                        .ToArray(),
                    ["Second"] = Enumerable.Range(1, 12)
                        .Select(static value => $"B-{value}")
                        .ToArray()
                }
        };

        IReadOnlyList<EventFilter> partitions =
            EventFilterPartitioner.Partition(filter);

        Assert.True(partitions.Count > 1);
        Assert.All(partitions, static partition => {
            Assert.Equal(
                new[] { "First", "Second" },
                partition.NamedData!.Keys
                    .OrderBy(static key => key)
                    .ToArray());
            Assert.InRange(
                EventFilterCompiler.CountExpressions(partition),
                1,
                EventFilterCompiler.MaximumXPathExpressions);
        });
        HashSet<(string First, string Second)> combinations =
            partitions
                .SelectMany(partition =>
                    partition.NamedData!["First"]
                        .SelectMany(first =>
                            partition.NamedData["Second"]
                                .Select(second =>
                                    (first, second))))
                .ToHashSet();
        Assert.Equal(12 * 12, combinations.Count);
    }

    [Fact]
    public void PartitionerRejectsExcessiveCartesianExpansionBeforeAllocation() {
        var filter = new EventFilter {
            NamedData = Enumerable.Range(1, 11)
                .ToDictionary(
                    index => "Field" + index,
                    index =>
                        (IReadOnlyList<string>)Enumerable
                            .Range(1, 100)
                            .Select(value =>
                                $"Value-{index}-{value}")
                            .ToArray())
        };

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EventFilterPartitioner.Partition(
                    filter));

        Assert.Contains(
            EventFilterPartitioner.MaximumPartitions
                .ToString(),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PartitionerRejectsNamedDataWhoseRequiredKeysCannotFit() {
        var filter = new EventFilter {
            NamedData = Enumerable.Range(1, 12)
                .ToDictionary(
                    index => "Field" + index,
                    index =>
                        (IReadOnlyList<string>)new[] {
                            "Value" + index
                        })
        };

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EventFilterPartitioner.Partition(filter));

        Assert.Contains(
            "require at least 24",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CloneCreatesIndependentCollections() {
        var source = new EventFilter {
            EventIds = new[] { 4625 },
            NamedData = new Dictionary<string, IReadOnlyList<string>> {
                ["TargetUserName"] = new[] { "alice" }
            }
        };

        EventFilter clone = source.Clone();
        ((int[])source.EventIds!)[0] = 4624;
        ((string[])source.NamedData!["TargetUserName"])[0] = "bob";

        Assert.Equal(4625, Assert.Single(clone.EventIds!));
        Assert.Equal("alice", Assert.Single(clone.NamedData!["TargetUserName"]));
    }
}