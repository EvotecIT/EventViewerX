using System;
using System.Collections;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace EventViewerX.Tests {
    public class TestWinEventFilter {
        [Fact]
        public void NamedDataFilterSingleValue() {
            var ht = new Hashtable { { "FieldName", "Value1" } };
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(namedDataFilter: [ht], logName: "xx", xpathOnly: true);
            Assert.Equal("*[EventData[Data[@Name='FieldName'] = 'Value1']]", result);
        }

        [Fact]
        public void NamedDataFilterTwoValues() {
            var ht = new Hashtable { { "FieldName", new[] { "Value1", "Value2" } } };
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(namedDataFilter: [ht], logName: "xx", xpathOnly: true);
            Assert.Equal("*[EventData[Data[@Name='FieldName'] = 'Value1' or Data[@Name='FieldName'] = 'Value2']]", result);
        }

        [Fact]
        public void NamedDataFilterCombinesDistinctKeysWithAnd() {
            var ht = new Hashtable {
                { "User", new[] { "Alice", "Bob" } },
                { "Address", "10.0.0.1" }
            };

            string result =
                WindowsEventFilterBuilder.BuildWinEventFilter(
                    namedDataFilter: [ht],
                    logName: "xx",
                    xpathOnly: true);

            Assert.Contains(
                "Data[@Name='User'] = 'Alice' or Data[@Name='User'] = 'Bob'",
                result,
                StringComparison.Ordinal);
            Assert.Contains(
                "and",
                result,
                StringComparison.Ordinal);
            Assert.Contains(
                "Data[@Name='Address'] = '10.0.0.1'",
                result,
                StringComparison.Ordinal);
        }

        [Fact]
        public void NamedDataFilterEscapesSpecialCharacters() {
            var ht = new Hashtable { { "Field", "O'Reilly & Co" } };
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(namedDataFilter: [ht], logName: "xx", xpathOnly: true);
            Assert.Equal("*[EventData[Data[@Name='Field'] = \"O'Reilly & Co\"]]", result);
        }

        [Fact]
        public void NamedDataExcludeFilterSingleValue() {
            var ht = new Hashtable { { "FieldName", "Value1" } };
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(namedDataExcludeFilter: [ht], logName: "xx");
            XElement query = XDocument.Parse(result).Root!.Element("Query")!;

            Assert.Equal("*", query.Element("Select")!.Value);
            Assert.Equal(
                "*[EventData[Data[@Name='FieldName'] = 'Value1']]",
                query.Element("Suppress")!.Value);
        }

        [Fact]
        public void NamedDataExcludeFilterTwoValues() {
            var ht = new Hashtable { { "FieldName", new[] { "Value1", "Value2" } } };
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(namedDataExcludeFilter: [ht], logName: "xx");
            XElement query = XDocument.Parse(result).Root!.Element("Query")!;

            Assert.Equal(
                "*[EventData[Data[@Name='FieldName'] = 'Value1' or Data[@Name='FieldName'] = 'Value2']]",
                query.Element("Suppress")!.Value);
        }

        [Fact]
        public void NamedDataExcludeFilterCombinesDistinctKeysWithAnd() {
            var ht = new Hashtable {
                { "User", "Alice" },
                { "Address", "10.0.0.1" }
            };

            string result =
                WindowsEventFilterBuilder.BuildWinEventFilter(
                    namedDataExcludeFilter: [ht],
                    logName: "xx");
            XElement query =
                XDocument.Parse(result)
                    .Root!
                    .Element("Query")!;

            Assert.Contains(
                "and",
                query.Element("Suppress")!.Value,
                StringComparison.Ordinal);
        }

        [Fact]
        public void NamedDataExcludeFilterRejectsUnsafeXpathOnlyProjection() {
            var ht = new Hashtable { { "FieldName", "Value1" } };

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                WindowsEventFilterBuilder.BuildWinEventFilter(
                    namedDataExcludeFilter: [ht],
                    xpathOnly: true));

            Assert.Contains("Suppress", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PathQueryUsesFilePrefix() {
            var ht = new Hashtable { { "param4", "BITS" } };
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(id: ["7040"], namedDataExcludeFilter: [ht], path: "C:/file.evtx");
            Assert.Contains(
                "Path=\"" +
                EventLogStructuredQueryParser
                    .CreateFileSourceIdentity(
                        "C:/file.evtx") +
                "\"",
                result);
            Assert.DoesNotContain("Select Path", result);
        }

        [Fact]
        public void LogNameQueryAddsSelectPath() {
            var ht = new Hashtable { { "param4", "BITS" } };
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(id: ["7040"], namedDataExcludeFilter: [ht], logName: "System");
            Assert.Contains("Path=\"System\"", result);
            Assert.Contains("Select Path=\"System\"", result);
        }

        [Fact]
        public void IdMultipleValuesOr() {
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(id: ["1", "2"], logName: "x", xpathOnly: true);
            Assert.Equal("*[System[(EventID=1) or (EventID=2)]]", result);
        }

        [Fact]
        public void EventIdZeroIsAcceptedForManifestProviders() {
            string result = WindowsEventFilterBuilder.BuildWinEventFilter(
                id: ["0", "65535"],
                excludeId: ["0"],
                xpathOnly: true);

            Assert.Contains("EventID=0", result, StringComparison.Ordinal);
            Assert.Contains("EventID=65535", result, StringComparison.Ordinal);
            Assert.Contains("EventID!=0", result, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("Verbose", 5)]
        [InlineData("16", 16)]
        [InlineData("255", 255)]
        public void EventLevelsAcceptStandardNamesAndCustomNumericValues(
            string level,
            int expected) {

            string result = WindowsEventFilterBuilder.BuildWinEventFilter(
                level: [level],
                xpathOnly: true);

            Assert.Contains(
                $"Level={expected}",
                result,
                StringComparison.Ordinal);
        }

        [Fact]
        public void IdMultipleValuesXmlQuery() {
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(id: ["1", "2"], logName: "Log");
            Assert.StartsWith("<QueryList>", result);
            Assert.Contains("(EventID=1) or\n(EventID=2)", result);
        }

        [Fact]
        public void MultipleExcludedIdsAreJoinedWithAnd() {
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(excludeId: ["1", "2"], xpathOnly: true);

            Assert.Equal("*[System[(EventID!=1) and (EventID!=2)]]", result);
        }

        [Fact]
        public void DateRangeFilterXpathOnly() {
            var start = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 7, 17, 10, 30, 0, DateTimeKind.Utc);
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(startTime: start, endTime: end, logName: "x", xpathOnly: true);
            Assert.Contains("TimeCreated[@SystemTime>='2026-07-17T10:00:00.0000000Z']", result);
            Assert.Contains("TimeCreated[@SystemTime<='2026-07-17T10:30:00.0000000Z']", result);
            Assert.DoesNotContain("<QueryList>", result);
        }

        [Fact]
        public void FutureStartTimeRemainsAnAbsoluteBoundary() {
            var start = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(startTime: start, logName: "x", xpathOnly: true);
            Assert.Contains("@SystemTime>='2030-01-02T03:04:05.0000000Z'", result);
            Assert.DoesNotContain("timediff", result);
        }

        [Fact]
        public void FutureEndTimeRemainsAnAbsoluteBoundary() {
            var end = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(endTime: end, logName: "x", xpathOnly: true);
            Assert.Contains("@SystemTime<='2030-01-02T03:04:05.0000000Z'", result);
            Assert.DoesNotContain("timediff", result);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("2147483648")]
        [InlineData("1 or EventID=2")]
        [InlineData("")]
        public void InvalidEventIdIsRejectedBeforeBuildingXpath(string value) {
            Assert.Throws<ArgumentException>(() => WindowsEventFilterBuilder.BuildWinEventFilter(id: [value], xpathOnly: true));
        }

        [Fact]
        public void EventIdsAboveTheWindowsRangeAreRejected() {
            Assert.Throws<ArgumentException>(() =>
                WindowsEventFilterBuilder
                    .BuildWinEventFilter(
                        id: ["65536"],
                        xpathOnly: true));
            Assert.Throws<ArgumentException>(() =>
                WindowsEventFilterBuilder
                    .BuildWinEventFilter(
                        excludeId: ["65536"],
                        xpathOnly: true));
        }

        [Fact]
        public void ProviderNameEscapesSpecialCharactersWinFilter() {
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(providerName: ["O'Reilly & Co"], logName: "x", xpathOnly: true);
            Assert.Equal("*[System[Provider[@Name=\"O'Reilly & Co\"]]]", result);
        }

        [Fact]
        public void QueryListXmlEncodesRawXpathAndPreservesItsMeaning() {
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(
                providerName: ["O'Reilly & Co"],
                startTime: new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc),
                logName: "Company & Product/Log");

            XDocument document = XDocument.Parse(result);
            XElement select = document.Root!.Element("Query")!.Element("Select")!;

            Assert.Equal("Company & Product/Log", select.Attribute("Path")!.Value);
            Assert.Contains("Provider[@Name=\"O'Reilly & Co\"]", select.Value);
            Assert.Contains("@SystemTime>='2026-07-17T10:00:00.0000000Z'", select.Value);
        }

        [Fact]
        public void ValuesContainingBothQuoteKindsAreRejected() {
            Assert.Throws<ArgumentException>(() => WindowsEventFilterBuilder.BuildWinEventFilter(
                providerName: ["Provider 'quoted' as \"other\""],
                xpathOnly: true));
        }

        [Fact]
        public void NullFiltersReturnWildcard() {
            var result = WindowsEventFilterBuilder.BuildWinEventFilter(xpathOnly: true);
            Assert.Equal("*", result);
        }

        [Fact]
        public void MinimumRecordIdIsEmittedAsNativeExclusiveBoundary() {
            string result = WindowsEventFilterBuilder.BuildWinEventFilter(
                minimumEventRecordIdExclusive: 123,
                xpathOnly: true);

            Assert.Equal("*[System[EventRecordID>123]]", result);
        }

        [Fact]
        public void MaximumRecordIdIsEmittedAsNativeExclusiveBoundary() {
            string result = WindowsEventFilterBuilder.BuildWinEventFilter(
                maximumEventRecordIdExclusive: 123,
                xpathOnly: true);

            Assert.Equal("*[System[EventRecordID<123]]", result);
        }

        [Fact]
        public void FilterRejectsMoreThanTheNativeXpathExpressionBudget() {
            var namedData = new Hashtable {
                { "Correlation", Enumerable.Range(1, EventFilterCompiler.MaximumXPathExpressions + 1).Select(static value => value.ToString()).ToArray() }
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() => WindowsEventFilterBuilder.BuildWinEventFilter(
                namedDataFilter: [namedData],
                xpathOnly: true));

            Assert.Contains(EventFilterCompiler.MaximumXPathExpressions.ToString(), exception.Message);
        }

        [Fact]
        public void NamedDataBudgetCountsBothNameAndValueComparisons() {
            var withinBudget = new Hashtable {
                { "Correlation", Enumerable.Range(1, EventFilterCompiler.MaximumXPathExpressions / 2).Select(static value => value.ToString()).ToArray() }
            };
            var overBudget = new Hashtable {
                { "Correlation", Enumerable.Range(1, EventFilterCompiler.MaximumXPathExpressions / 2 + 1).Select(static value => value.ToString()).ToArray() }
            };

            string xpath = WindowsEventFilterBuilder.BuildWinEventFilter(namedDataFilter: [withinBudget], xpathOnly: true);
            Assert.NotEmpty(xpath);
            Assert.Throws<ArgumentException>(() => WindowsEventFilterBuilder.BuildWinEventFilter(
                namedDataFilter: [overBudget],
                xpathOnly: true));
        }
    }
}
