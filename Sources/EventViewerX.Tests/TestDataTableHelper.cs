using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using EventViewerX.Helpers;
using Xunit;

namespace EventViewerX.Tests;

public partial class TestDataTableHelper {
    private class SimpleEvent {
        public int Id { get; set; }
        public string Name { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }

    [Fact]
    public void CachesTypeMembers() {
        var helperType = typeof(DataTableHelper);
        var cacheField = helperType.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(cacheField);

        object cache = cacheField!.GetValue(null)!;
        MethodInfo clearMethod = cacheField.FieldType.GetMethod("Clear")!;
        clearMethod.Invoke(cache, null);

        var method = helperType.GetMethod("ToDataTableInternal", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var generic = method!.MakeGenericMethod(typeof(SimpleEvent));

        var items = new List<SimpleEvent> {
            new() { Id = 1, Name = "A", Data = new() { { "Key", "Value" } } }
        };

        generic.Invoke(null, new object[] { items });

        PropertyInfo countProperty = cacheField.FieldType.GetProperty("Count")!;
        Assert.Equal(1, (int)countProperty.GetValue(cache)!);
        PropertyInfo indexer = cacheField.FieldType.GetProperty("Item")!;
        var first = indexer.GetValue(cache, new object[] { typeof(SimpleEvent) });

        generic.Invoke(null, new object[] { items });

        Assert.Equal(1, (int)countProperty.GetValue(cache)!);
        var second = indexer.GetValue(cache, new object[] { typeof(SimpleEvent) });

        Assert.Same(first, second);
    }
}

public partial class TestDataTableHelper {
        private class Dummy {
            public int? IntValue { get; set; }
            public DateTime? DateValue { get; set; }
            public double? DoubleValue;
        }

        [Fact]
        public void NullablePrimitivePropertiesHandled() {
            var items = new[] {
                new Dummy {
                    IntValue = 5,
                    DateValue = new DateTime(2024, 1, 1),
                    DoubleValue = 1.5
                },
                new Dummy()
            };

            var method = typeof(DataTableHelper).GetMethod("ToDataTableInternal", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var generic = method!.MakeGenericMethod(typeof(Dummy));
            var table = (DataTable)generic.Invoke(null, new object[] { items });

            Assert.Equal(3, table.Columns.Count);
            Assert.Equal(typeof(int), table.Columns["IntValue"].DataType);
            Assert.Equal(typeof(DateTime), table.Columns["DateValue"].DataType);
            Assert.Equal(typeof(double), table.Columns["DoubleValue"].DataType);

            Assert.Equal(2, table.Rows.Count);
            Assert.Equal(5, table.Rows[0]["IntValue"]);
            Assert.Equal(new DateTime(2024, 1, 1), table.Rows[0]["DateValue"]);
            Assert.Equal(1.5, table.Rows[0]["DoubleValue"]);
            Assert.Equal(DBNull.Value, table.Rows[1]["IntValue"]);
            Assert.Equal(DBNull.Value, table.Rows[1]["DateValue"]);
            Assert.Equal(DBNull.Value, table.Rows[1]["DoubleValue"]);
        }
    }
