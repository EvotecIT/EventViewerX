using System.Diagnostics.Eventing.Reader;
using System.IO.Compression;
using System.Security.Principal;
using System.Xml.Linq;
using EventViewerX.Reporting;
using EventViewerX.Rules.ActiveDirectory;
using EventViewerX.Rules.HyperV;
using EventViewerX.Storage;
using HtmlForgeX;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventDefinitionAndReporting {
    [Fact]
    public void TypedProjectionPlansRemainScopedToTheEventDefinition() {
        EventTypeDefinition first = EventTypeCatalog.GetDefinition(EventType.OSStartup);
        var second = new EventTypeDefinition(
            EventType.OSShutdown,
            "Shared shutdown",
            "Second definition using the same projected CLR type.",
            first.Category,
            first.Sources,
            first.Fields,
            first.RecordType,
            Array.Empty<EventType>());

        EventReportSectionDefinition firstSection = EventReportProjectionFactory.Create(
            first.RecordType!,
            first);
        EventReportSectionDefinition secondSection = EventReportProjectionFactory.Create(
            first.RecordType!,
            second);

        Assert.Equal(nameof(EventType.OSStartup), firstSection.Name);
        Assert.Equal(nameof(EventType.OSShutdown), secondSection.Name);
        Assert.NotEqual(firstSection.Key, secondSection.Key);
    }

    [Fact]
    public async Task TypedReportRowsUseCatalogDefinitionNamesInsteadOfLegacyRecordLabels() {
        EventObject computer = CreateSecuritySource(4741, 41);
        EventObject user = CreateSecuritySource(4720, 42);
        foreach (EventObject source in new[] { computer, user }) {
            source.Data["OldUacValue"] = "-";
            source.Data["NewUacValue"] = "-";
            source.Data["UserAccountControl"] = "-";
        }
        object[] records = {
            new ADComputerCreateChange(computer),
            new ADUserCreateChange(user),
            new ADUserStatus(CreateSecuritySource(4722, 43)),
            new VmCheckpointCreated(CreateSecuritySource(4096, 44))
        };

        EventReport report = EventReportEngine.Create(records);

        string[] expected = {
            nameof(EventType.ADComputerCreateChange),
            nameof(EventType.ADUserCreateChange),
            nameof(EventType.ADUserStatus),
            nameof(EventType.HyperVCheckpointCreated)
        };
        Assert.Equal(expected.OrderBy(static value => value),
            report.Rows.Select(static row => row.Type).OrderBy(static value => value));
        Assert.All(report.Rows, row => Assert.Contains(report.Sections,
            section => string.Equals(section.Name, row.Type, StringComparison.Ordinal)));

        string storePath = Path.Combine(Path.GetTempPath(), $"evx-catalog-identities-{Guid.NewGuid():N}.db");
        try {
            EventStoreWriteResult write = await new EventStore(storePath).WriteAsync(report);
            Assert.Equal(4, write.Inserted);
        } finally {
            foreach (string suffix in new[] { string.Empty, "-wal", "-shm" }) {
                File.Delete(storePath + suffix);
            }
        }
    }

    [Theory]
    [InlineData("ADUserLogonFailed")]
    [InlineData("activedirectoryauthentication")]
    [InlineData("Generic")]
    [InlineData("EventStoreSummary")]
    public void CustomDefinitionsRejectReservedBuiltInTypeNames(string name) {
        EventDefinition definition = CreateDefinition();
        definition.Name = name;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(definition.Validate);

        Assert.Contains(name, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reserved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomDefinitionsCanonicalizeStableNamesFieldsAliasesAndSources() {
        EventDefinition definition = CreateDefinition();
        definition.Name = "  ServiceChanges  ";
        definition.Sources[0].LogName = "  Security  ";
        definition.Sources[0].ProviderNames = new[] { "  Provider.One  " };
        definition.Fields[0].Name = "  Who  ";
        definition.Fields[0].Aliases = new[] { "  Account  " };
        definition.Fields[0].SourceName = "  TargetUserName  ";

        definition.Validate();
        EventPredicateBuilder builder = EventPredicateBuilder.ForDefinition(definition);

        Assert.Equal("ServiceChanges", definition.Name);
        Assert.Equal("Security", definition.Sources[0].LogName);
        Assert.Equal("Provider.One", Assert.Single(definition.Sources[0].ProviderNames));
        Assert.Equal("Who", definition.Fields[0].Name);
        Assert.Equal("Account", Assert.Single(definition.Fields[0].Aliases));
        Assert.Equal("TargetUserName", definition.Fields[0].SourceName);
        Assert.Equal("Who", builder.Field("Who").Name);
        Assert.Equal("Who", builder.Field(" Account ").Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CustomDefinitionsRejectReservedOutputMetadataFieldsAndAliases(bool useFieldName) {
        EventDefinition definition = CreateDefinition();
        if (useFieldName) {
            definition.Fields[0].Name = EventDefinition.OutputMetadataFieldName;
        } else {
            definition.Fields[0].Aliases = new[] { EventDefinition.OutputMetadataFieldName };
        }

        InvalidDataException exception = Assert.Throws<InvalidDataException>(definition.Validate);

        Assert.Contains(EventDefinition.OutputMetadataFieldName, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reserved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyTypedAndCustomQueriesRetainSchemasForCsvExport() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventReportRequest typedRequest = EventReportRequest.ForTypes(EventType.OSStartup);
        typedRequest.Paths = new[] { fixture };
        typedRequest.RecordIds = new[] { long.MaxValue };
        EventDefinition customDefinition = CreateDefinition();
        EventReportRequest customRequest = EventReportRequest.ForDefinition(customDefinition);
        customRequest.Paths = new[] { fixture };
        customRequest.RecordIds = new[] { long.MaxValue };

        EventReport typed = await EventReportEngine.QueryAsync(typedRequest);
        EventReport custom = await EventReportEngine.QueryAsync(customRequest);
        string typedCsv = Path.Combine(Path.GetTempPath(), $"evx-empty-typed-{Guid.NewGuid():N}.csv");
        string customCsv = Path.Combine(Path.GetTempPath(), $"evx-empty-custom-{Guid.NewGuid():N}.csv");
        try {
            Assert.Empty(typed.Rows);
            Assert.Empty(custom.Rows);
            Assert.Single(typed.Sections);
            Assert.Single(custom.Sections);
            EventReportCsvRenderer.Save(typed, typedCsv);
            EventReportCsvRenderer.Save(custom, customCsv);
            Assert.NotEmpty(File.ReadAllText(typedCsv));
            Assert.StartsWith("User,Computer", File.ReadAllText(customCsv), StringComparison.Ordinal);
        } finally {
            File.Delete(typedCsv);
            File.Delete(customCsv);
        }
    }

    [Fact]
    public void CustomDefinitionRoundTripsCompilesAndProjects() {
        EventDefinition definition = CreateDefinition();
        string path = Path.Combine(Path.GetTempPath(), $"evx-definition-{Guid.NewGuid():N}.json");
        try {
            definition.Save(path);
            EventDefinition loaded = EventDefinition.Load(path);
            string xml = EventDefinitionCompiler.BuildQueryXml(loaded);
            Assert.Contains("Path=\"Security\"", xml, StringComparison.Ordinal);
            Assert.Contains("EventID=4625", xml, StringComparison.Ordinal);

            var source = new EventObject(new SyntheticEventRecord(), "WEC01", EventReadMode.StructuredDataAndMessage) {
                ContainerLog = "ForwardedEvents",
                GatheredLogName = "ForwardedEvents"
            };
            source.Data["TargetUserName"] = "alice";
            CustomEventRecord record = EventDefinitionEngine.CreateRecord(loaded, source);
            Assert.Equal("FailedLogonCustom", record.TypeName);
            Assert.Equal("alice", record.Values["User"]);
            Assert.Equal("source.ad.evotec.xyz", record.Values["Computer"]);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OneSnapshotRendersHtmlExcelAndEmail() {
        var source = new EventObject(new SyntheticEventRecord(), "WEC01", EventReadMode.StructuredDataAndMessage) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };
        source.Data["TargetUserName"] = "alice";
        CustomEventRecord custom = EventDefinitionEngine.CreateRecord(CreateDefinition(), source);
        EventReport report = EventReportEngine.Create(new object[] { custom }, "Failed logons");
        Assert.Single(report.Rows);
        EventReportSection customSection = Assert.Single(report.Sections);
        Assert.Equal(EventReportSectionKind.Custom, customSection.Kind);
        Assert.Equal(new[] { "User", "Computer" }, customSection.Columns.Select(static column => column.Name));
        Assert.DoesNotContain(customSection.Columns, static column => column.Name == nameof(EventReportRow.EventId));
        Assert.Equal("Security", report.Rows[0].SourceLog);
        Assert.Equal("ForwardedEvents", report.Rows[0].ContainerLog);

        string html = EventReportHtmlRenderer.Render(report);
        Assert.Contains("data-hfx-monitoring-shell=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-nav=\"overview\"", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-record-explorer=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-column-picker-toggle=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-inline-details=\"false\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-hfx-monitoring-inline-details=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-record-drawer-tab=\"detail-provenance\">Provenance</button>", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-record-drawer-detail-group=\"provenance\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("hfx-report-workspace", html, StringComparison.Ordinal);
        Assert.Contains("Failed logons", html, StringComparison.Ordinal);
        Assert.Contains("alice", html, StringComparison.Ordinal);

        string topDrawerHtml = EventReportHtmlRenderer.Render(report, new EventReportHtmlOptions {
            RecordDrawerPlacement = MonitoringRecordDrawerPlacement.Top
        });
        Assert.Contains("data-hfx-monitoring-record-drawer-placement=\"top\"", topDrawerHtml, StringComparison.Ordinal);

        string workbook = Path.Combine(Path.GetTempPath(), $"evx-report-{Guid.NewGuid():N}.xlsx");
        string csv = Path.Combine(Path.GetTempPath(), $"evx-report-{Guid.NewGuid():N}.csv");
        try {
            Assert.Equal(workbook, EventReportExcelRenderer.Save(report, workbook));
            using ZipArchive archive = ZipFile.OpenRead(workbook);
            Assert.Contains(archive.Entries, static entry => entry.FullName == "xl/workbook.xml");
            Assert.Contains(archive.Entries, static entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal));
            Assert.Equal(csv, EventReportCsvRenderer.Save(report, csv));
            string csvText = File.ReadAllText(csv);
            Assert.StartsWith("User,Computer", csvText, StringComparison.Ordinal);
            Assert.DoesNotContain("Event ID", csvText, StringComparison.Ordinal);
        } finally {
            File.Delete(workbook);
            File.Delete(csv);
        }

        EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
        Assert.Contains("Failed logons", email.Subject, StringComparison.Ordinal);
        Assert.Contains("alice", email.Html, StringComparison.Ordinal);
        Assert.NotEmpty(email.PlainText);
    }

    [Fact]
    public async Task TypedReportsUseDefinitionFieldsAndKeepDifferentTypesInSeparateSections() {
        EventObject successfulSource = CreateSecuritySource(4624);
        successfulSource.Data["TargetDomainName"] = "EVOTEC";
        successfulSource.Data["TargetUserName"] = "alice";
        successfulSource.Data["SubjectDomainName"] = "EVOTEC";
        successfulSource.Data["SubjectUserName"] = "service.account";
        successfulSource.Data["IpAddress"] = "10.0.0.20";
        successfulSource.Data["IpPort"] = "55123";
        successfulSource.Data["LogonType"] = "3";

        EventObject failedSource = CreateSecuritySource(4625);
        failedSource.Data["TargetDomainName"] = "EVOTEC";
        failedSource.Data["TargetUserName"] = "bob";
        failedSource.Data["WorkstationName"] = "CLIENT01";
        failedSource.Data["IpAddress"] = "10.0.0.21";
        failedSource.Data["IpPort"] = "55124";
        failedSource.Data["FailureReason"] = "%%2304";

        EventReport report = EventReportEngine.Create(new object[] {
            new ADUserLogon(successfulSource),
            new ADUserLogonFailed(failedSource)
        }, "Authentication activity");

        Assert.Equal(2, report.Sections.Count);
        EventReportSection successful = Assert.Single(report.Sections,
            static section => section.Name == nameof(EventType.ADUserLogon));
        EventReportSection failed = Assert.Single(report.Sections,
            static section => section.Name == nameof(EventType.ADUserLogonFailed));
        Assert.Equal(EventReportSectionKind.Typed, successful.Kind);
        Assert.Contains(successful.Columns, static column => column.Name == "Who");
        Assert.Contains(successful.Columns, static column => column.Name == "When");
        Assert.Contains(successful.Columns, static column => column.Name == "IpAddress");
        Assert.Contains(successful.Columns, static column => column.DisplayName == "IP Address");
        Assert.DoesNotContain(successful.Columns, static column => column.Name is "EventId" or "Provider" or "EventIds" or "LogName" or "Type");
        Assert.Equal("EVOTEC\\alice", successful.Rows[0].Values["ObjectAffected"]);
        Assert.DoesNotContain(nameof(EventReportRow.EventId), successful.Rows[0].Values.Keys);
        Assert.Contains(failed.Columns, static column => column.Name == "FailureReason");

        string html = EventReportHtmlRenderer.Render(report);
        Assert.Contains("AD User Logon", html, StringComparison.Ordinal);
        Assert.Contains("AD User Logon Failed", html, StringComparison.Ordinal);
        Assert.Contains("Object Affected", html, StringComparison.Ordinal);
        Assert.Contains("EVOTEC\\alice", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-nav=\"aduserlogon\"", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-nav=\"aduserlogonfailed\"", html, StringComparison.Ordinal);
        Assert.Contains("data-hfx-monitoring-record-toggle", html, StringComparison.Ordinal);
        Assert.Contains("Source computer", html, StringComparison.Ordinal);
        Assert.Contains("ForwardedEvents", html, StringComparison.Ordinal);

        EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
        Assert.Contains("AD User Logon", email.Html, StringComparison.Ordinal);
        Assert.Contains("AD User Logon Failed", email.Html, StringComparison.Ordinal);
        Assert.Contains("EVOTEC\\alice", email.Html, StringComparison.Ordinal);

        string workbook = Path.Combine(Path.GetTempPath(), $"evx-typed-report-{Guid.NewGuid():N}.xlsx");
        string csvBundle = Path.Combine(Path.GetTempPath(), $"evx-typed-report-{Guid.NewGuid():N}.zip");
        try {
            EventReportExcelRenderer.Save(report, workbook);
            using ZipArchive archive = ZipFile.OpenRead(workbook);
            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XDocument[] tables = archive.Entries
                .Where(static entry => entry.FullName.StartsWith("xl/tables/table", StringComparison.Ordinal))
                .Select(static entry => {
                    using Stream stream = entry.Open();
                    return XDocument.Load(stream);
                }).ToArray();
            Assert.Contains(tables, table => TableColumns(table, spreadsheet).Contains("Object Affected"));
            Assert.Contains(tables, table => TableColumns(table, spreadsheet).Contains("Failure Reason"));
            Assert.Contains(tables, table => TableColumns(table, spreadsheet).Contains("Event ID"));
            Assert.DoesNotContain(tables.Where(table => TableColumns(table, spreadsheet).Contains("Object Affected")),
                table => TableColumns(table, spreadsheet).Contains("Event ID"));
            EventReportCsvRenderer.Save(report, csvBundle);
            using ZipArchive csvArchive = ZipFile.OpenRead(csvBundle);
            Assert.Contains(csvArchive.Entries, static entry => entry.FullName == "ADUserLogon.csv");
            Assert.Contains(csvArchive.Entries, static entry => entry.FullName == "ADUserLogonFailed.csv");
            Assert.Contains(csvArchive.Entries, static entry => entry.FullName == "event-provenance.csv");
            Assert.Contains(csvArchive.Entries, static entry => entry.FullName == "coverage.csv");
            Assert.Contains(csvArchive.Entries, static entry => entry.FullName == "manifest.json");
            ZipArchiveEntry successfulCsv = Assert.Single(
                csvArchive.Entries,
                static entry => entry.FullName == "ADUserLogon.csv");
            using var reader = new StreamReader(successfulCsv.Open());
            string csvText = reader.ReadToEnd();
            Assert.Contains("Object Affected", csvText, StringComparison.Ordinal);
            Assert.DoesNotContain("Failure Reason", csvText, StringComparison.Ordinal);
            Assert.DoesNotContain("Event ID", csvText, StringComparison.Ordinal);
        } finally {
            File.Delete(workbook);
            File.Delete(csvBundle);
        }
    }

    [Fact]
    public async Task CompositeEmailReservesDigestRowsForEveryPopulatedType() {
        EventObject successfulSource = CreateSecuritySource(4624);
        EventObject failedSource = CreateSecuritySource(4625);
        object[] events = Enumerable.Repeat<object>(new ADUserLogon(successfulSource), 30)
            .Concat(new object[] { new ADUserLogonFailed(failedSource) })
            .ToArray();
        EventReport report = EventReportEngine.Create(events, "Authentication activity");

        EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report, maximumRows: 25);

        Assert.Contains("AD User Logon", email.Html, StringComparison.Ordinal);
        Assert.Contains("AD User Logon Failed", email.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TypedCollectionFieldsRenderAsReadableValues() {
        EventObject source = CreateSecuritySource(4672);
        source.Data["SubjectDomainName"] = "EVOTEC";
        source.Data["SubjectUserName"] = "alice";
        source.Data["PrivilegeList"] = "SeSecurityPrivilege\r\n\tSeBackupPrivilege";
        EventReport report = EventReportEngine.Create(new object[] {
            new ADUserPrivilegeUse(source)
        }, "Privilege use");

        string html = EventReportHtmlRenderer.Render(report);
        EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);

        Assert.Contains("SeSecurityPrivilege", html, StringComparison.Ordinal);
        Assert.Contains("SeBackupPrivilege", html, StringComparison.Ordinal);
        Assert.Contains("SeSecurityPrivilege", email.Html, StringComparison.Ordinal);
        Assert.Contains("SeBackupPrivilege", email.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Collections.Generic.List", html, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Collections.Generic.List", email.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateDisplayNamesRemainDistinctAcrossEveryRenderer() {
        EventDefinition definition = new() {
            Name = "DuplicateHeadings",
            DisplayName = "Duplicate headings",
            Sources = CreateDefinition().Sources,
            Fields = new[] {
                new EventDefinitionField {
                    Name = "FirstValue",
                    DisplayName = "Value",
                    Source = EventFieldSource.Data,
                    SourceName = "First"
                },
                new EventDefinitionField {
                    Name = "SecondValue",
                    DisplayName = "Value",
                    Source = EventFieldSource.Data,
                    SourceName = "Second"
                }
            }
        };
        EventObject source = CreateSecuritySource(4625);
        source.Data["First"] = "one";
        source.Data["Second"] = "two";
        EventReport report = EventReportEngine.Create(new object[] {
            EventDefinitionEngine.CreateRecord(definition, source)
        }, "Duplicate headings");

        string html = EventReportHtmlRenderer.Render(report);
        EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
        string workbook = Path.Combine(Path.GetTempPath(), $"evx-duplicate-headings-{Guid.NewGuid():N}.xlsx");
        string csv = Path.Combine(Path.GetTempPath(), $"evx-duplicate-headings-{Guid.NewGuid():N}.csv");
        try {
            EventReportCsvRenderer.Save(report, csv);
            EventReportExcelRenderer.Save(report, workbook);
            string csvText = File.ReadAllText(csv);
            using ZipArchive archive = ZipFile.OpenRead(workbook);
            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            string[][] tableColumns = archive.Entries
                .Where(static entry => entry.FullName.StartsWith("xl/tables/table", StringComparison.Ordinal))
                .Select(entry => {
                    using Stream stream = entry.Open();
                    return TableColumns(XDocument.Load(stream), spreadsheet);
                }).ToArray();

            Assert.StartsWith("Value,Value Second Value", csvText, StringComparison.Ordinal);
            Assert.Contains("one,two", csvText, StringComparison.Ordinal);
            Assert.Contains("Value Second Value", html, StringComparison.Ordinal);
            Assert.Contains("one", html, StringComparison.Ordinal);
            Assert.Contains("two", html, StringComparison.Ordinal);
            Assert.Contains("Value Second Value", email.Html, StringComparison.Ordinal);
            Assert.Contains(tableColumns, static columns =>
                columns.SequenceEqual(new[] { "Value", "Value Second Value" }));
        } finally {
            File.Delete(workbook);
            File.Delete(csv);
        }
    }

    [Fact]
    public void WideTypedDefinitionKeepsItsOwnColumnsWithoutFallingBackToGenericDetails() {
        EventDefinition definition = new() {
            Name = "WideProviderEvent",
            Sources = CreateDefinition().Sources,
            Fields = Enumerable.Range(1, 13)
                .Select(index => new EventDefinitionField {
                    Name = $"Field{index}",
                    Source = EventFieldSource.Data,
                    SourceName = $"Value{index}"
                })
                .ToArray()
        };
        var source = new EventObject(new SyntheticEventRecord(), "WEC01", EventReadMode.StructuredDataAndMessage) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };
        for (int index = 1; index <= 13; index++) {
            source.Data[$"Value{index}"] = $"value{index}";
        }
        EventReport report = EventReportEngine.Create(new object[] {
            EventDefinitionEngine.CreateRecord(definition, source)
        }, "Wide provider event");

        string html = EventReportHtmlRenderer.Render(report);
        Assert.Contains("Field13", html, StringComparison.Ordinal);
        Assert.Contains("value13", html, StringComparison.Ordinal);

        string workbook = Path.Combine(Path.GetTempPath(), $"evx-wide-report-{Guid.NewGuid():N}.xlsx");
        try {
            EventReportExcelRenderer.Save(report, workbook);
            using ZipArchive archive = ZipFile.OpenRead(workbook);
            XDocument eventTable = archive.Entries
                .Where(static entry => entry.FullName.StartsWith("xl/tables/table", StringComparison.Ordinal))
                .Select(static entry => {
                    using Stream stream = entry.Open();
                    return XDocument.Load(stream);
                })
                .Single(document => TableColumns(document,
                    "http://schemas.openxmlformats.org/spreadsheetml/2006/main").Contains("Field13"));
            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            string[] columns = eventTable.Descendants(spreadsheet + "tableColumn")
                .Select(static column => column.Attribute("name")?.Value ?? string.Empty)
                .ToArray();

            Assert.Equal(13, columns.Length);
            Assert.Contains("Field13", columns);
            Assert.DoesNotContain("Details", columns);
        } finally {
            File.Delete(workbook);
        }
    }

    [Fact]
    public void DefinitionRejectsDuplicateFieldsAndInvalidEventIds() {
        EventDefinition definition = CreateDefinition();
        definition.Sources = new[] { new EventDefinitionSource { LogName = "Security", EventIds = new[] { 0 } } };
        definition.Fields = new[] {
            new EventDefinitionField { Name = "User", Source = EventFieldSource.Data, SourceName = "TargetUserName" },
            new EventDefinitionField { Name = "user", Source = EventFieldSource.Message }
        };
        InvalidDataException exception = Assert.Throws<InvalidDataException>(definition.Validate);
        Assert.Contains("EventIds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefinitionRejectsInvalidConfiguredTypedLiteralsBeforeReadingEvents() {
        EventDefinition constant = CreateDefinition();
        constant.Fields = new[] {
            new EventDefinitionField {
                Name = "Attempts",
                Source = EventFieldSource.Constant,
                SourceName = "not-a-number",
                ValueKind = EventFieldValueKind.Int32
            }
        };
        EventDefinition fallback = CreateDefinition();
        fallback.Fields = new[] {
            new EventDefinitionField {
                Name = "OccurredAt",
                Source = EventFieldSource.Data,
                SourceName = "OccurredAt",
                DefaultValue = "not-a-date",
                ValueKind = EventFieldValueKind.DateTime
            }
        };

        InvalidDataException constantError = Assert.Throws<InvalidDataException>(constant.Validate);
        InvalidDataException fallbackError = Assert.Throws<InvalidDataException>(fallback.Validate);

        Assert.Contains("Fields[0].SourceName", constantError.Message, StringComparison.Ordinal);
        Assert.Contains("Fields[0].DefaultValue", fallbackError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TypedReportRequestsRejectGenericEventIdSelectorsBeforeReadingEvents() {
        EventReportRequest builtIn = EventReportRequest.ForTypes(EventType.ADUserLogonFailed);
        builtIn.EventIds = new[] { 4625 };
        EventReportRequest custom = EventReportRequest.ForDefinition(CreateDefinition());
        custom.EventIds = new[] { 4625 };

        InvalidOperationException builtInError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => EventReportEngine.QueryAsync(builtIn));
        InvalidOperationException customError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => EventReportEngine.QueryAsync(custom));

        Assert.Contains("typed definitions own source event IDs", builtInError.Message, StringComparison.Ordinal);
        Assert.Contains("typed definitions own source event IDs", customError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericPredicateRowsReserveNativeMetadataAliases() {
        DateTime time = new(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc);
        var row = new EventReportRow {
            Type = "Generic",
            EventId = 4624,
            RecordId = 42,
            Provider = "Microsoft-Windows-Security-Auditing",
            SourceLog = "Security",
            TimeCreated = time,
            Values = new Dictionary<string, object?> {
                ["Id"] = "untrusted-id",
                ["ProviderName"] = "untrusted-provider",
                ["LogName"] = "untrusted-log",
                ["When"] = "untrusted-time"
            }
        };

        IReadOnlyDictionary<string, object?> values = row.ToPredicateDictionary();

        Assert.Equal(4624, values["Id"]);
        Assert.Equal("Microsoft-Windows-Security-Auditing", values["ProviderName"]);
        Assert.Equal("Security", values["LogName"]);
        Assert.Equal(time, values["When"]);
    }

    [Fact]
    public void CsvBundleReservesMetadataEntryNamesFromCustomSections() {
        EventDefinition coverage = CreateDefinition();
        coverage.Name = "coverage";
        coverage.DisplayName = "Coverage events";
        EventDefinition provenance = CreateDefinition();
        provenance.Name = "event-provenance";
        provenance.DisplayName = "Provenance events";
        var source = new EventObject(
            new SyntheticEventRecord(),
            "WEC01",
            EventReadMode.StructuredDataAndMessage);
        source.Data["TargetUserName"] = "alice";
        EventReport report = EventReportEngine.Create(new object[] {
            EventDefinitionEngine.CreateRecord(coverage, source),
            EventDefinitionEngine.CreateRecord(provenance, source)
        });
        string path = Path.Combine(Path.GetTempPath(), $"evx-reserved-csv-{Guid.NewGuid():N}.zip");

        try {
            EventReportCsvRenderer.Save(report, path);
            using ZipArchive archive = ZipFile.OpenRead(path);
            string[] names = archive.Entries.Select(static entry => entry.FullName).ToArray();

            Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains("coverage.csv", names);
            Assert.Contains("event-provenance.csv", names);
            Assert.Contains("coverage-2.csv", names);
            Assert.Contains("event-provenance-2.csv", names);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void CsvRawCompanionHeadersCannotCollideWithDeclaredHeaders() {
        var row = new EventReportRow {
            Type = "HeaderCollision",
            Values = new Dictionary<string, object?> {
                ["OperationType"] = "%%14674",
                ["ExistingRaw"] = "domain value"
            }
        };
        EventValueNormalizationEngine.Populate(row);
        var schema = new EventReportSectionSchema {
            Name = "HeaderCollision",
            DisplayName = "Header collision",
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                new EventReportColumnSchema {
                    Name = "OperationType",
                    DisplayName = "Status",
                    ValueTypeName = EventReportColumnSchema.GetStableTypeName(typeof(string))
                },
                new EventReportColumnSchema {
                    Name = "ExistingRaw",
                    DisplayName = "Status Raw",
                    ValueTypeName = EventReportColumnSchema.GetStableTypeName(typeof(string))
                }
            }
        };
        EventReport report = EventReportEngine.CreateStored(new[] { row }, new[] { schema });
        string path = Path.Combine(Path.GetTempPath(), $"evx-csv-header-{Guid.NewGuid():N}.csv");

        try {
            EventReportCsvRenderer.Save(report, path);
            string header = File.ReadLines(path).First();

            Assert.Equal(3, header.Split(',').Length);
            Assert.Contains("Status Raw", header, StringComparison.Ordinal);
            Assert.Contains("Status Raw (2)", header, StringComparison.Ordinal);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void DefinitionSnapshotFreezesMutableSourcesFieldsAndRecordIds() {
        EventDefinition definition = CreateDefinition();
        var query = new EventDefinitionQuery(definition) {
            RecordIds = new long[] { 41, 42 },
            MachineNames = new string?[] { " WEC01 " },
            MaxCandidates = 25,
            ReadMode = EventReadMode.Full,
            BufferCapacity = 32,
            RemoteConnectionTimeoutMilliseconds = 1500
        };

        EventDefinitionQuery snapshot = EventDefinitionEngine.CreateSnapshot(query);
        definition.Sources = new[] { new EventDefinitionSource { LogName = "System", EventIds = new[] { 41 } } };
        definition.Fields = Array.Empty<EventDefinitionField>();
        query.RecordIds = new long[] { 99 };

        Assert.Equal("Security", snapshot.Definition.Sources[0].LogName);
        Assert.Equal(new[] { 4625 }, snapshot.Definition.Sources[0].EventIds);
        Assert.Equal(new long[] { 41, 42 }, snapshot.RecordIds);
        Assert.Equal("WEC01", snapshot.MachineNames![0]);
        Assert.Equal(2, snapshot.Definition.Fields.Count);
        Assert.Equal(25, snapshot.MaxCandidates);
        Assert.Equal(EventReadMode.Full, snapshot.ReadMode);
        Assert.Equal(32, snapshot.BufferCapacity);
        Assert.Equal(1500, snapshot.RemoteConnectionTimeoutMilliseconds);
    }

    [Fact]
    public void CompilerNormalizesDuplicatesAndRejectsInvalidSourceContracts() {
        string xpath = EventDefinitionCompiler.BuildSourceXPath(
            "Security", new[] { 4625, 4625 }, new[] { "Provider", "provider" });

        Assert.Equal(1, xpath.Split("EventID=4625", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, xpath.Split("@Name='Provider'", StringSplitOptions.None).Length - 1);
        Assert.Throws<ArgumentException>(() =>
            EventDefinitionCompiler.BuildSourceXPath("Security", Array.Empty<int>()));
        Assert.Throws<ArgumentException>(() =>
            EventDefinitionCompiler.BuildSourceXPath("Security", new[] { 1 }, new[] { " " }));
    }

    [Fact]
    public void CompositeSubscriptionQueriesStayWithinTheNativeExpressionLimit() {
        string queryXml = EventDefinitionCompiler.BuildQueryXml(
            new[] { EventType.ActiveDirectoryChanges });
        XDocument query = XDocument.Parse(queryXml);
        XElement[] securitySelects = query
            .Descendants("Select")
            .Where(static select => string.Equals(
                select.Attribute("Path")?.Value,
                "Security",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(securitySelects.Length > 1);
        Assert.All(securitySelects, static select =>
            Assert.InRange(
                select.Value.Split("EventID=", StringSplitOptions.None).Length - 1,
                1,
                EventFilterCompiler.MaximumXPathExpressions));

        int[] actualIds = securitySelects
            .SelectMany(static select => System.Text.RegularExpressions.Regex
                .Matches(select.Value, @"EventID=(\d+)")
                .Select(static match => int.Parse(
                    match.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture)))
            .Distinct()
            .OrderBy(static eventId => eventId)
            .ToArray();
        int[] expectedIds = EventTypeCatalog
            .GetSources(new[] { EventType.ActiveDirectoryChanges })
            .Single(static source => string.Equals(
                source.LogName,
                "Security",
                StringComparison.OrdinalIgnoreCase))
            .EventIds
            .OrderBy(static eventId => eventId)
            .ToArray();
        Assert.Equal(expectedIds, actualIds);
    }

    [Fact]
    public void CreateRowProducesAcyclicNormalizedWatcherPayload() {
        var source = new EventObject(new SyntheticEventRecord(), "WEC01", EventReadMode.StructuredDataAndMessage) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };
        source.Data["TargetUserName"] = "alice";
        CustomEventRecord custom = EventDefinitionEngine.CreateRecord(CreateDefinition(), source);

        EventReportRow row = EventReportEngine.CreateRow(custom);

        Assert.Equal("FailedLogonCustom", row.Type);
        Assert.Equal("alice", row.Values["User"]);
        Assert.DoesNotContain(row.GetType().GetProperties(), static property =>
            property.PropertyType == typeof(EventObject));
    }

    [Fact]
    public async Task OfflineReportUsesTheSharedBatchEngineAndTracksFileCoverage() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventReportRequest request = EventReportRequest.ForFiles(fixture);
        request.MaxEvents = 3;
        request.Oldest = true;

        EventReport report = await EventReportEngine.QueryAsync(request);

        Assert.Equal(3, report.Rows.Count);
        EventReportSection section = Assert.Single(report.Sections);
        Assert.Equal(EventReportSectionKind.Generic, section.Kind);
        Assert.Contains(section.Columns, static column => column.Name == nameof(EventReportRow.EventId));
        Assert.Contains(section.Columns, static column => column.Name == nameof(EventReportRow.Provider));
        Assert.Single(report.Coverage);
        Assert.Equal("Offline", report.Coverage[0].MachineName);
        Assert.Equal(fixture, report.Coverage[0].LogName);
    }

    [Fact]
    public async Task GenericAndCustomReportsExposeResultLimitIncompleteness() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventReportRequest genericRequest = EventReportRequest.ForFiles(fixture);
        genericRequest.MaxEvents = 1;
        EventDefinition definition = new() {
            Name = "ServiceStartTypeChange",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "System",
                    EventIds = new[] { 7040 },
                    ProviderNames = new[] { "Service Control Manager" }
                }
            }
        };
        EventReportRequest customRequest = EventReportRequest.ForDefinition(definition);
        customRequest.Paths = new[] { fixture };
        customRequest.MaxEvents = 1;

        EventReport generic = await EventReportEngine.QueryAsync(genericRequest);
        EventReport custom = await EventReportEngine.QueryAsync(customRequest);

        Assert.Single(generic.Rows);
        Assert.Single(custom.Rows);
        Assert.Equal(2, generic.EventsScanned);
        Assert.Equal(2, custom.EventsScanned);
        Assert.True(generic.ScanLimitReached);
        Assert.True(custom.ScanLimitReached);
        Assert.Contains("MaxEvents", generic.CompletenessDiagnostic, StringComparison.Ordinal);
        Assert.Contains("MaxEvents", custom.CompletenessDiagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportRequestsApplyDurableRecordBoundariesToEveryQueryOwner() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        string? genericMachine = null;
        string? genericContainer = null;
        EventReportRequest generic = EventReportRequest.ForFiles(fixture);
        generic.MinimumRecordIdExclusiveResolver = (machine, container) => {
            genericMachine = machine;
            genericContainer = container;
            return long.MaxValue;
        };
        EventReportRequest typed = EventReportRequest.ForTypes(EventType.OSStartup);
        typed.Paths = new[] { fixture };
        typed.MinimumRecordIdExclusiveResolver = (_, _) => long.MaxValue;
        EventReportRequest custom = EventReportRequest.ForDefinition(CreateDefinition());
        custom.Paths = new[] { fixture };
        custom.MinimumRecordIdExclusiveResolver = (_, _) => long.MaxValue;

        EventReport genericReport = await EventReportEngine.QueryAsync(generic);
        EventReport typedReport = await EventReportEngine.QueryAsync(typed);
        EventReport customReport = await EventReportEngine.QueryAsync(custom);

        Assert.Empty(genericReport.Rows);
        Assert.Empty(typedReport.Rows);
        Assert.Empty(customReport.Rows);
        Assert.Equal(fixture, genericMachine);
        Assert.Equal(fixture, genericContainer);
    }

    [Fact]
    public async Task GenericReportsResumeNativelyAfterADurableBookmark() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventObject first = Assert.IsType<EventObject>(EventLogEngine.ReadFile(
            new EventLogFileQuery(fixture) {
                Oldest = true,
                MaxEvents = 1,
                ReadMode = EventReadMode.Metadata,
                IncludeBookmark = true
            }).Single());
        string bookmark = Assert.IsType<string>(first.BookmarkXml);
        EventReportRequest request = EventReportRequest.ForFiles(fixture);
        request.Oldest = true;
        request.BookmarkXmlResolver = (_, _) => bookmark;
        EventReportRequest completeRequest = EventReportRequest.ForFiles(fixture);
        completeRequest.Oldest = true;

        EventReport complete = await EventReportEngine.QueryAsync(completeRequest);
        EventReport report = await EventReportEngine.QueryAsync(request);

        Assert.Equal(complete.Rows.Count - 1, report.Rows.Count);
        Assert.DoesNotContain(
            report.Rows,
            row => row.RecordId == first.RecordId);
    }

    [Fact]
    public void CollectorQueriesSeekFromBookmarkBeforeManagedCandidateLimits() {
        const string bookmark =
            "<BookmarkList><Bookmark Channel='ForwardedEvents' RecordId='42' IsCurrent='true' /></BookmarkList>";
        var typedQuery = new EventTypeQuery(new[] { EventType.ADUserLogon }) {
            MachineNames = new[] { "WEC01" },
            CollectorLogName = "ForwardedEvents",
            MaxCandidates = 100,
            BookmarkXmlResolver = (_, _) => bookmark
        };
        var eventInfo = new Dictionary<string, HashSet<int>>(
            StringComparer.OrdinalIgnoreCase) {
            ["Security"] = new HashSet<int> { 4624 }
        };
        EventLogBatchQuery typedBatch = EventTypeEngine.CreateCollectorBatch(
            typedQuery,
            eventInfo,
            new EventTypeQueryExecutionInfo(),
            startTime: null,
            endTime: null);
        EventDefinition definition = new() {
            Name = "CustomLogon",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "Security",
                    EventIds = new[] { 4624 }
                }
            },
            Fields = new[] {
                new EventDefinitionField {
                    Name = "User",
                    Source = EventFieldSource.Data,
                    SourceName = "TargetUserName"
                }
            }
        };
        var customQuery = new EventDefinitionQuery(definition) {
            MachineNames = new[] { "WEC01" },
            CollectorLogName = "ForwardedEvents",
            MaxCandidates = 100,
            BookmarkXmlResolver = (_, _) => bookmark
        };
        EventLogBatchQuery customBatch = EventDefinitionEngine.CreateCollectorBatch(
            customQuery,
            new EventDefinitionQueryExecutionInfo(),
            new[] { "WEC01" },
            start: null,
            end: null);

        foreach (EventLogChannelQuery channel in new[] {
                     Assert.Single(typedBatch.ChannelQueries),
                     Assert.Single(customBatch.ChannelQueries)
                 }) {
            Assert.Equal("*", channel.XPath);
            Assert.Equal(bookmark, channel.BookmarkXml);
            Assert.Equal(1, channel.BookmarkOffset);
            Assert.True(channel.StrictBookmark);
            Assert.Equal(100, channel.ManagedMaxEventsScanned);
        }
    }

    [Fact]
    public async Task CustomDefinitionOwnsSemanticsWhenReadingOfflineFiles() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventDefinition definition = new() {
            Name = "ServiceStartTypeChange",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "System",
                    EventIds = new[] { 7040 },
                    ProviderNames = new[] { "Service Control Manager" }
                }
            },
            Fields = new[] {
                new EventDefinitionField { Name = "ServiceName", Source = EventFieldSource.Data, SourceName = "param1" }
            }
        };
        EventReportRequest request = EventReportRequest.ForDefinition(definition);
        request.Paths = new[] { fixture };
        request.MaxEvents = 2;

        EventReport report = await EventReportEngine.QueryAsync(request);

        Assert.Equal(2, report.Rows.Count);
        Assert.All(report.Rows, static row => Assert.Equal("ServiceStartTypeChange", row.Type));
        Assert.Single(report.Coverage);
        Assert.Equal("Offline", report.Coverage[0].MachineName);
    }

    [Fact]
    public async Task ReportRequestRejectsConcurrencyOutsideTheCoreEngineContract() {
        EventReportRequest request = EventReportRequest.ForLog("System");
        request.MaxConcurrency = EventLogLimits.MaximumConcurrency + 1;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => EventReportEngine.QueryAsync(request));

        Assert.Contains("MaxConcurrency must be between", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomDefinitionDoesNotReportResultTruncationAtNaturalCompletion() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventDefinition definition = new() {
            Name = "ServiceStartTypeChange",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "System",
                    EventIds = new[] { 7040 },
                    ProviderNames = new[] { "Service Control Manager" }
                }
            }
        };
        var all = new List<CustomEventRecord>();
        await foreach (CustomEventRecord record in EventDefinitionEngine.ReadAsync(
                           new EventDefinitionQuery(definition) { Paths = new[] { fixture } })) {
            all.Add(record);
        }
        Assert.True(all.Count >= 2);
        long selectedRecordId = all[^1].SourceEvent.RecordId!.Value;
        var query = new EventDefinitionQuery(definition) {
            Paths = new[] { fixture },
            MaxEvents = 1,
            ResultPredicate = record => record.SourceEvent.RecordId == selectedRecordId
        };
        var actual = new List<CustomEventRecord>();
        var info = new EventDefinitionQueryExecutionInfo();

        await foreach (CustomEventRecord record in EventDefinitionEngine.ReadAsync(query, info)) {
            actual.Add(record);
        }

        Assert.Single(actual);
        Assert.Equal(selectedRecordId, actual[0].SourceEvent.RecordId);
        Assert.False(info.ResultLimitReached);
        Assert.False(info.ScanLimitReached);
    }

    [Fact]
    public async Task CustomDefinitionReportsResultTruncationOnlyWhenAnotherMatchExists() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventDefinition definition = new() {
            Name = "ServiceStartTypeChange",
            Sources = new[] {
                new EventDefinitionSource {
                    LogName = "System",
                    EventIds = new[] { 7040 },
                    ProviderNames = new[] { "Service Control Manager" }
                }
            }
        };
        var observed = new List<long?>();
        var query = new EventDefinitionQuery(definition) {
            Paths = new[] { fixture },
            MaxEvents = 1,
            CandidateObserver = source => observed.Add(source.RecordId)
        };
        var actual = new List<CustomEventRecord>();
        var info = new EventDefinitionQueryExecutionInfo();

        await foreach (CustomEventRecord record in EventDefinitionEngine.ReadAsync(query, info)) {
            actual.Add(record);
        }

        CustomEventRecord onlyRecord = Assert.Single(actual);
        Assert.True(info.ResultLimitReached);
        Assert.False(info.ScanLimitReached);
        Assert.Equal(2, info.EventsScanned);
        Assert.Equal(onlyRecord.SourceEvent.RecordId, Assert.Single(observed));
    }

    [Fact]
    public async Task CustomDefinitionReportsCandidateTruncationOnlyWhenAnotherCandidateExists() {
        string fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventDefinition definition = new() {
            Name = "ServiceStartTypeChange",
            Sources = new[] {
                new EventDefinitionSource { LogName = "System", EventIds = new[] { 7040 } }
            }
        };
        var query = new EventDefinitionQuery(definition) {
            Paths = new[] { fixture },
            MaxCandidates = 1,
            ResultPredicate = static _ => false
        };
        var info = new EventDefinitionQueryExecutionInfo();

        await foreach (CustomEventRecord _ in EventDefinitionEngine.ReadAsync(query, info)) {
        }

        Assert.Equal(1, info.EventsScanned);
        Assert.Equal(0, info.EventsEmitted);
        Assert.True(info.ScanLimitReached);
    }

    [Fact]
    public void OfflineDefinitionSnapshotFreezesPathsAndRejectsRemoteMixing() {
        string[] paths = { "one.evtx", "two.evtx" };
        var query = new EventDefinitionQuery(CreateDefinition()) { Paths = paths };

        EventDefinitionQuery snapshot = EventDefinitionEngine.CreateSnapshot(query);
        paths[0] = "changed.evtx";

        Assert.Equal(new[] { "one.evtx", "two.evtx" }, snapshot.Paths);
        query.MachineNames = new[] { "server" };
        Assert.Throws<ArgumentException>(() => EventDefinitionEngine.CreateSnapshot(query));
    }

    [Fact]
    public void CustomReportSectionsRejectConflictingValueTypeRevisionsForOneDefinitionName() {
        EventDefinition CreateRevision(EventFieldValueKind kind) => new() {
            Name = "RevisionAudit",
            Sources = new[] {
                new EventDefinitionSource { LogName = "Security", EventIds = new[] { 4625 } }
            },
            Fields = new[] {
                new EventDefinitionField {
                    Name = "Value",
                    ValueKind = kind,
                    Source = EventFieldSource.Data,
                    SourceName = "Value"
                }
            }
        };
        EventObject firstSource = CreateSecuritySource(4625, 71);
        EventObject secondSource = CreateSecuritySource(4625, 72);
        firstSource.Data["Value"] = "1";
        secondSource.Data["Value"] = "2";
        object[] records = {
            EventDefinitionEngine.CreateRecord(CreateRevision(EventFieldValueKind.String), firstSource),
            EventDefinitionEngine.CreateRecord(CreateRevision(EventFieldValueKind.Int32), secondSource)
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EventReportEngine.Create(records));

        Assert.Contains("conflicting schema revisions", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RevisionAudit", exception.Message, StringComparison.Ordinal);
    }

    private static EventDefinition CreateDefinition() => new() {
        Name = "FailedLogonCustom",
        DisplayName = "Custom failed logons",
        Sources = new[] {
            new EventDefinitionSource {
                LogName = "Security",
                EventIds = new[] { 4625 },
                ProviderNames = new[] { "Microsoft-Windows-Security-Auditing" }
            }
        },
        Fields = new[] {
            new EventDefinitionField { Name = "User", Source = EventFieldSource.Data, SourceName = "TargetUserName" },
            new EventDefinitionField { Name = "Computer", Source = EventFieldSource.Metadata, SourceName = "SourceComputer" }
        }
    };

    private static EventObject CreateSecuritySource(int eventId, long recordId = 42) => new(
        new SyntheticEventRecord(eventId, recordId), "WEC01", EventReadMode.StructuredDataAndMessage) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };

    private static string[] TableColumns(XDocument table, XNamespace spreadsheet) => table
        .Descendants(spreadsheet + "tableColumn")
        .Select(static column => column.Attribute("name")?.Value ?? string.Empty)
        .ToArray();

    private sealed class SyntheticEventRecord : EventRecord {
        private readonly int _eventId;
        private readonly long _recordId;

        internal SyntheticEventRecord(int eventId = 4625, long recordId = 42) {
            _eventId = eventId;
            _recordId = recordId;
        }

        public override string ProviderName => "Microsoft-Windows-Security-Auditing";
        public override string LogName => "Security";
        public override string MachineName => "source.ad.evotec.xyz";
        public override int Id => _eventId;
        public override byte? Level => 0;
        public override int? Task => 12544;
        public override long? Keywords => 0;
        public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();
        public override short? Opcode => 0;
        public override string OpcodeDisplayName => string.Empty;
        public override string TaskDisplayName => string.Empty;
        public override Guid? ProviderId => null;
        public override Guid? ActivityId => null;
        public override Guid? RelatedActivityId => null;
        public override int? ProcessId => 1;
        public override int? ThreadId => 1;
        public override string LevelDisplayName => "Information";
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        public override int? Qualifiers => null;
        public override long? RecordId => _recordId;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;
        public override string FormatDescription() => "An account failed to log on.";
        public override string FormatDescription(IEnumerable<object> values) => FormatDescription();
        public override string ToXml() => "<Event><EventData><Data Name=\"TargetUserName\">alice</Data></EventData></Event>";
    }
}
