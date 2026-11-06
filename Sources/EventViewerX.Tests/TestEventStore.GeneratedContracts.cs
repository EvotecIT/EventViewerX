using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Theory]
    [InlineData(typeof(GeneratedStoreDetails), true)]
    [InlineData(typeof(int?[]), true)]
    [InlineData(typeof(IReadOnlyList<long>), false)]
    [InlineData(typeof(IReadOnlyList<byte>), false)]
    public async Task GeneratedContractsRestoreLegacyAndBuiltInSchemaColumnTypes(Type valueType, bool legacy) {
        string path = CreateStorePath();
        try {
            JsonTypeInfo[] contracts = valueType == typeof(GeneratedStoreDetails)
                ? new[] { GeneratedStoreContext.Default.GeneratedStoreDetails }
                : Array.Empty<JsonTypeInfo>();
            var schema = new EventReportSectionSchema {
                Name = "GeneratedStoreValues",
                Kind = EventReportSectionKind.Custom,
                Columns = new[] {
                    new EventReportColumnSchema {
                        Name = "Value",
                        ValueTypeName = legacy ? valueType.AssemblyQualifiedName! : valueType.FullName!
                    }
                }
            };
            var store = new EventStore(path, contracts);
            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { schema.Name }, DefinitionSchemas = new[] { schema }
            });
            Assert.Empty(report.Rows);
            Assert.Equal(valueType, Assert.Single(Assert.Single(report.Sections).Columns).ValueType);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task GeneratedStoreContractsPreserveSortedSetContainsPredicates() {
        string path = CreateStorePath();
        try {
            var schema = new EventReportSectionSchema {
                Name = "SetValues", Kind = EventReportSectionKind.Custom,
                Columns = new[] { CreateColumn("Numbers", typeof(SortedSet<int>)) }
            };
            EventReport report = EventReportEngine.CreateStored(new[] {
                new EventReportRow {
                    Type = schema.Name, RecordId = 42,
                    TimeCreated = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc),
                    Values = new Dictionary<string, object?> { ["Numbers"] = new SortedSet<int> { 1, 7 } }
                },
                new EventReportRow {
                    Type = schema.Name, RecordId = 43,
                    TimeCreated = new DateTime(2026, 11, 6, 0, 0, 1, DateTimeKind.Utc),
                    Values = new Dictionary<string, object?> { ["Numbers"] = new SortedSet<int> { 2, 3 } }
                }
            }, new[] { schema });
            var store = new EventStore(path, new[] { GeneratedStoreContext.Default.GetTypeInfo(typeof(SortedSet<int>))! });
            await store.WriteAsync(report);
            EventReport selected = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { schema.Name },
                Predicate = EventPredicate.Compare("Numbers", EventPredicateOperator.Contains, 7)
            });
            Assert.Equal(42, Assert.Single(selected.Rows).RecordId);
            Assert.Equal(new[] { 1, 7 }, Assert.IsType<SortedSet<int>>(selected.Rows[0].Values["Numbers"]));
        } finally {
            DeleteStore(path);
        }
    }
}

internal sealed class GeneratedStoreDetails {
    public int Code { get; set; }
}

[JsonSerializable(typeof(GeneratedStoreDetails))]
[JsonSerializable(typeof(SortedSet<int>))]
internal partial class GeneratedStoreContext : JsonSerializerContext { }
