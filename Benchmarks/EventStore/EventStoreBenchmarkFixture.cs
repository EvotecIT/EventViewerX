#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using EventViewerX.Reporting;

/// <summary>Creates deterministic typed rows outside the measured benchmark operations.</summary>
public static class EventStoreBenchmarkFixture {
    /// <summary>Creates one homogeneous benchmark report with stable identities and values.</summary>
    public static EventReport CreateReport(int rowCount) {
        if (rowCount <= 0 || rowCount % 10 != 0) {
            throw new ArgumentOutOfRangeException(
                nameof(rowCount),
                "The benchmark row count must be a positive multiple of ten.");
        }

        var rows = new EventReportRow[rowCount];
        DateTime start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < rowCount; index++) {
            rows[index] = new EventReportRow {
                TimeCreated = start.AddSeconds(index),
                Type = "BenchmarkEvent",
                EventId = 41000,
                RecordId = index + 1L,
                Provider = "EventViewerX-Benchmark",
                SourceLog = "Security",
                ContainerLog = "ForwardedEvents",
                SourceComputer = "source-" + (index % 4).ToString(CultureInfo.InvariantCulture),
                CollectorComputer = "WEC01",
                Level = "Information",
                Message = "Benchmark event " + index.ToString(CultureInfo.InvariantCulture),
                Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                    ["User"] = "user-" + (index % 10).ToString(CultureInfo.InvariantCulture),
                    ["Computer"] = "computer-" + (index % 100).ToString(CultureInfo.InvariantCulture)
                }
            };
        }

        var schema = new EventReportSectionSchema {
            Name = "BenchmarkEvent",
            DisplayName = "Benchmark events",
            Description = "Synthetic homogeneous benchmark rows.",
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                Column("User"),
                Column("Computer")
            }
        };
        return EventReportEngine.CreateStored(
            rows,
            new[] { schema },
            "Event store benchmark");
    }

    private static EventReportColumnSchema Column(string name) => new() {
        Name = name,
        DisplayName = name,
        ValueTypeName = typeof(string).AssemblyQualifiedName!
    };
}
