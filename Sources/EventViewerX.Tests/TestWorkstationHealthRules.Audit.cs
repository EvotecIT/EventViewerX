using EventViewerX.Reporting;
using EventViewerX.Rules.CertificateAuthority;
using EventViewerX.Rules.Windows;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestWorkstationHealthRules {
    [Theory]
    [InlineData("The World Wide Web Publishing Service did not register the URL prefix http://*:8080/ for site 2.")]
    [InlineData("Nie można zarejestrować adresu http://*:8080/ dla witryny 2.")]
    public void IISBindingFallbackDoesNotTreatTheNumericSiteIdAsASiteName(string message) {
        EventObject source = new SavedEventRecord {
            ProviderName = "Microsoft-Windows-IIS-W3SVC", Channel = "System", EventId = 1007,
            TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            Message = message,
            MessageRenderStatus = EventMessageRenderStatus.Rendered,
            Data = new Dictionary<string, string> { ["SiteID"] = "2" }
        }.ToEventObject("iis-schema-fixture", EventReadMode.StructuredDataAndMessage);
        var record = Assert.IsType<EventViewerX.Rules.IIS.IISSiteBindingFailure>(
            EventTypeCatalog.CreateEventRule(source, new[] { EventType.IISSiteBindingFailure }));
        Assert.Equal("http://*:8080/", record.Binding);
        Assert.Equal("2", record.SiteId);
        Assert.Empty(record.SiteName);
    }

    [Fact]
    public void FindingTitleUsesItsStableFieldBeforeAnotherColumnsDisplayLabel() {
        var rows = new[] { "", "Other A", "Other B" }.Select(alias => new EventReportRow {
            Type = "DetectionFinding", TimeCreated = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            Values = new Dictionary<string, object?> { ["Alias"] = alias, ["Title"] = "Actual finding title" }
        }).ToArray();
        var section = new EventReportSection("DetectionFinding", "Detection findings", "", EventReportSectionKind.Custom,
            new[] { new EventReportColumn("Alias", "Title", typeof(string)), new EventReportColumn("Title", "Title", typeof(string)) }, rows);
        EventReportPresentationSection presentation = EventReportPresentationProjection.Create(section);
        Assert.Equal("Actual finding title", EventReportPresentationProjection.BuildTitle(presentation, presentation.Rows[0], rows[0]));
    }

    [Fact]
    public void DetectionOverviewUsesFindingSeverityEvenWhenTheSourceIsInformational() {
        var source = Source("network-monitor-service-7045");
        EventTypeRecord typed = Assert.IsType<NetworkMonitorDriverLoaded>(
            EventTypeCatalog.CreateEventRule(source, new[] { EventType.NetworkMonitorDriverLoaded }));
        EventDetectionPlan plan = EventDetectionPlan.Compile(EventDetectionCatalog.GetBuiltInRules());
        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(new[] { EventObservation.Create(source, typed) }, plan),
            item => item.RuleId == "EVX-ENDPOINT-0010");
        Assert.Equal((byte)4, source.Level);
        Assert.Equal(EventDetectionSeverity.High, finding.Severity);
        EventReport report = EventReportEngine.Create(new object[] { finding });
        EventReportPresentationSection presentation = EventReportPresentationProjection.Create(Assert.Single(report.Sections));
        Assert.Equal(new[] { "Title", "Severity", "Status", "StartTimeUtc", "EvidenceCount" },
            presentation.PrimaryColumns.Select(column => column.Name));
        Assert.Equal(finding.Title, EventReportPresentationProjection.BuildTitle(presentation, presentation.Rows[0], report.Rows[0]));
        string html = EventReportHtmlRenderer.Render(report);
        Assert.Contains("1 high or critical finding", html);
        Assert.Contains("data-hfx-monitoring-filter-values=\"state:critical ", html);
        Assert.DoesNotContain("No error or warning levels recorded", html);
        report.Rows[0].Values = new Dictionary<string, object?>(report.Rows[0].Values) { ["Severity"] = 999 };
        string unknown = EventReportHtmlRenderer.Render(report);
        Assert.Contains("1 unknown severity", unknown);
        Assert.Contains("data-hfx-monitoring-filter-values=\"state:unknown ", unknown);
        Assert.DoesNotContain("data-hfx-monitoring-filter-values=\"state:healthy ", unknown);
    }

    [Fact]
    public void RowTimeMetricsAndRecentOrderingTreatUnspecifiedEventTimesAsUtc() {
        var newer = Project<ApplicationCrash>("application-crash-1000", EventType.ApplicationCrash);
        var older = Project<ApplicationCrash>("application-crash-1000", EventType.ApplicationCrash);
        newer.ApplicationName = "Newest application marker";
        older.ApplicationName = "Older application marker";
        EventReport report = EventReportEngine.Create(new object[] { older, newer });
        report.Rows[0].TimeCreated = new DateTime(2026, 11, 1, 11, 30, 0, DateTimeKind.Utc).ToLocalTime();
        report.Rows[1].TimeCreated = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Unspecified);
        string html = EventReportHtmlRenderer.Render(report);
        Assert.Contains("2026-11-01 11:30 UTC", html);
        Assert.Contains("2026-11-01 12:00 UTC", html);
        Assert.True(html.IndexOf("Newest application marker", StringComparison.Ordinal) <
            html.IndexOf("Older application marker", StringComparison.Ordinal));
    }

    [Fact]
    public void LogsFullSecurity_UsesTheKnownChannelWhenTheNativeTemplateHasNoPayload() {
        // The installed Eventlog 1104 template is empty.
        EventObject source = new SavedEventRecord {
            ProviderName = "Microsoft-Windows-Eventlog", Channel = "Security", EventId = 1104,
            Computer = "server-fixture", TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc)
        }.ToEventObject("security-full-fixture", EventReadMode.StructuredData);
        var record = Assert.IsType<EventViewerX.Rules.Logging.LogsFullSecurity>(
            EventTypeCatalog.CreateEventRule(source, new[] { EventType.LogsFullSecurity }));
        Assert.Equal("Security", record.LogType);
        Assert.Equal("Security log is full", record.Action);
        Assert.Empty(record.Who);
    }

    [Fact]
    public void LogsClearedOther_PreservesThePayloadChannelInsteadOfAnOperationToken() {
        EventObject source = new SavedEventRecord {
            ProviderName = "Microsoft-Windows-Eventlog", Channel = "System", EventId = 104,
            Computer = "server-fixture", TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            Data = new Dictionary<string, string> {
                ["SubjectUserName"] = "fixture-user", ["SubjectDomainName"] = "EXAMPLE",
                ["Channel"] = "Application", ["BackupPath"] = ""
            }
        }.ToEventObject("cleared-log-fixture", EventReadMode.StructuredData);
        var record = Assert.IsType<EventViewerX.Rules.Logging.LogsClearedOther>(
            EventTypeCatalog.CreateEventRule(source, new[] { EventType.LogsClearedOther }));
        Assert.Equal("Application", record.LogType);
        Assert.Equal("Event log cleared", record.Action);
        Assert.Empty(record.BackupPath);
    }

    [Fact]
    public void LogsClearedSecurity_PreservesTheKnownChannelWithoutInventingABackup() {
        // Native Eventlog 1102 template fields, with synthetic values and no rendered message.
        EventObject source = new SavedEventRecord {
            ProviderName = "Microsoft-Windows-Eventlog", Channel = "Security", EventId = 1102,
            Computer = "server-fixture", TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            Data = new Dictionary<string, string> {
                ["SubjectUserName"] = "fixture-user", ["SubjectDomainName"] = "EXAMPLE",
                ["SubjectUserSid"] = "S-1-5-18", ["SubjectLogonId"] = "0x1234"
            }
        }.ToEventObject("security-fixture", EventReadMode.StructuredData);
        var record = Assert.IsType<EventViewerX.Rules.Logging.LogsClearedSecurity>(
            EventTypeCatalog.CreateEventRule(source, new[] { EventType.LogsClearedSecurity }));
        Assert.Equal("Security", record.LogType);
        Assert.Equal("Audit log cleared", record.Action);
        Assert.Equal(@"EXAMPLE\fixture-user", record.Who);
        Assert.Empty(record.BackupPath);
    }

    [Fact]
    public void NetworkMonitorDriverLoaded_RejectsFilesystemFilterLoads() {
        Assert.Null(EventTypeCatalog.CreateEventRule(Source("filter-manager-driver-6"), new[] { EventType.NetworkMonitorDriverLoaded }));
        EventSourceDefinition source = Assert.Single(EventTypeCatalog.GetDefinition(EventType.NetworkMonitorDriverLoaded).Sources);
        Assert.DoesNotContain(6, source.EventIds);
        Assert.Equal("Service Control Manager", Assert.Single(source.ProviderNames));
    }

    [Fact]
    public void IISSiteBindingFailure_ProjectsNativeUrlAndSiteIdentifier() {
        var record = Project<EventViewerX.Rules.IIS.IISSiteBindingFailure>("iis-binding-failure-1007", EventType.IISSiteBindingFailure);
        Assert.Equal("http://*:8080/", record.Binding);
        Assert.Equal("2", record.SiteId);
        Assert.Empty(record.SiteName);
        Assert.Null(EventTypeCatalog.CreateEventRule(Source("iis-binding-failure-1007",
            xml => SystemElement(xml, "Provider").SetAttributeValue("Name", "Microsoft-Windows-Dhcp-Client")), new[] { EventType.IISSiteBindingFailure }));
    }

    [Theory]
    [InlineData(EventType.HyperVVirtualMachineStarted, 18500)]
    [InlineData(EventType.HyperVVirtualMachineShutdown, 18502)]
    public void HyperVLifecycle_UsesTheWorkerAdminChannel(EventType type, int eventId) {
        EventSourceDefinition source = Assert.Single(EventTypeCatalog.GetDefinition(type).Sources);
        Assert.Equal("Microsoft-Windows-Hyper-V-Worker-Admin", source.LogName);
        Assert.Equal(eventId, Assert.Single(source.EventIds));
        Assert.Equal("Microsoft-Windows-Hyper-V-Worker", Assert.Single(source.ProviderNames));
        Assert.DoesNotContain("18560", EventDefinitionCompiler.BuildQueryXml(new[] { type }));
    }

    [Fact]
    public void ApplicationGroupPolicy_UsesPreferenceProvidersRatherThanTheSystemProvider() {
        EventObject source = Source("iis-binding-failure-1007", xml => {
            SystemElement(xml, "Provider").SetAttributeValue("Name", "Group Policy Drive Maps");
            SystemElement(xml, "Channel").Value = "Application";
            SystemElement(xml, "EventID").Value = "4098";
        });
        Assert.IsType<ClientGroupPoliciesApplication>(EventTypeCatalog.CreateEventRule(source, new[] { EventType.ClientGroupPoliciesApplication }));
        Assert.True(new ClientGroupPoliciesApplication(source).CanHandle(source));
        Assert.Contains("Group Policy Drive Maps", EventDefinitionCompiler.BuildQueryXml(new[] { EventType.ClientGroupPoliciesApplication }));
        source = Source("iis-binding-failure-1007", xml => {
            SystemElement(xml, "Provider").SetAttributeValue("Name", "Microsoft-Windows-CAPI2");
            SystemElement(xml, "Channel").Value = "Application";
            SystemElement(xml, "EventID").Value = "4098";
        });
        Assert.Null(EventTypeCatalog.CreateEventRule(source, new[] { EventType.ClientGroupPoliciesApplication }));
    }

    [Fact]
    public void HtmlRecordDetails_PreserveTheCompleteMessage() {
        DfsReplicationError record = Project<DfsReplicationError>("dfs-replication-error-5002", EventType.DfsReplicationError);
        EventReport report = EventReportEngine.Create(new object[] { record });
        Assert.Single(report.Rows).Message = new string('x', 400) + " End of complete event message";
        string html = EventReportHtmlRenderer.Render(report);
        Assert.Contains("End of complete event message", html);
        Assert.DoesNotContain("Message excerpt", html);
    }

    [Theory]
    [InlineData("certificate-issued-4887-v0", "")]
    [InlineData("certificate-issued-4887-v1", "1234ABCD")]
    public void CertificateIssued_PreservesProviderTextAndSerialNumber(string fixture, string serialNumber) {
        CertificateIssued record = Project<CertificateIssued>(fixture, EventType.CertificateIssued);
        Assert.Equal("42", record.RequestId);
        Assert.Equal(@"EXAMPLE\Certificate User", record.Requester);
        Assert.Equal("CN=Fixture User, O=Example", record.Subject);
        Assert.Equal("User Enrollment", record.CertificateTemplate);
        Assert.Equal(serialNumber, record.SerialNumber);
    }

    [Fact]
    public void CertificateIssued_RejectsRequestsThatHaveOnlyBeenReceived() {
        Assert.Null(EventTypeCatalog.CreateEventRule(Source("certificate-request-4886"), new[] { EventType.CertificateIssued }));
    }

    [Fact]
    public void LogsClearedSecurity_RejectsAutomaticLogBackups() {
        Assert.Null(EventTypeCatalog.CreateEventRule(Source("security-log-backup-1105"), new[] { EventType.LogsClearedSecurity }));
    }

    [Fact]
    public void NetworkMonitorDriverLoaded_UsesStructuredServiceIdentityWithoutMessageResources() {
        NetworkMonitorDriverLoaded record = Project<NetworkMonitorDriverLoaded>("network-monitor-service-7045", EventType.NetworkMonitorDriverLoaded);
        Assert.Equal("Npcap Packet Driver (NPCAP)", record.DriverName);
        Assert.Equal("Driver service installed", record.Action);
        Assert.Equal("workstation-fixture", record.Computer);
    }

    [Fact]
    public void DfsReplicationError_ProjectsCapturedClassicInsertionStrings() {
        DfsReplicationError record = Project<DfsReplicationError>("dfs-replication-error-5002", EventType.DfsReplicationError);
        Assert.Equal("Example Replication Group", record.ReplicationGroup);
        Assert.Equal("replication-peer", record.Partner);
        Assert.Equal("1753", record.ErrorCode);
    }

    [Fact]
    public void ScheduledTaskCreated_ReadsNamespacedRegistrationMetadata() {
        ScheduledTaskCreated record = Project<ScheduledTaskCreated>("scheduled-task-created-4698", EventType.ScheduledTaskCreated);
        Assert.Equal(@"EXAMPLE\Scheduler", record.Author);
        Assert.Equal(new DateTime(2026, 11, 1, 12, 0, 0), record.Created);
        Assert.Equal(@"\Example\AuditTask", record.TaskName);
    }

    [Fact]
    public void DefenderConfigurationChanged_IdentifiesSettingFromChangedValues() {
        DefenderConfigurationChanged record = Project<DefenderConfigurationChanged>("defender-configuration-5007", EventType.DefenderConfigurationChanged);
        Assert.Equal(@"HKLM\SOFTWARE\Example\Protection\Enabled", record.Configuration);
        Assert.EndsWith(" = 0x1", record.OldValue);
        Assert.EndsWith(" = 0x0", record.NewValue);
    }

    [Fact]
    public void BitLockerKeyChange_RejectsGenericPrivilegedServiceAudit() {
        Assert.Null(EventTypeCatalog.CreateEventRule(Source("privileged-service-4673"), new[] { EventType.BitLockerKeyChange }));
    }

    [Fact]
    public void BitLockerKeyChange_ProjectsNativeProtectorIdentityAndType() {
        BitLockerKeyChange record = Project<BitLockerKeyChange>("bitlocker-key-protector-775", EventType.BitLockerKeyChange);
        Assert.Equal("Key protector created", record.Action);
        Assert.Equal("C:", record.VolumeMountPoint);
        Assert.Equal("{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}", record.ProtectorId);
        Assert.Equal(BitLockerProtectorType.NumericalPassword, record.ProtectorType);
        Assert.Equal("{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}", record.RecoveryKeyId);
        Assert.Empty(record.RecoveryDestination);
        Assert.Null(record.Volume);
    }

    [Fact]
    public void TypedReportsAndDiscovery_ExcludeRuleRoutingMetadata() {
        ApplicationCrash record = Project<ApplicationCrash>("application-crash-1000", EventType.ApplicationCrash);
        EventReport report = EventReportEngine.Create(new object[] { record });
        foreach (string name in new[] { "ProviderNames", "MatchPriority", "EventIds", "LogName", "Type" }) {
            Assert.DoesNotContain(EventTypeCatalog.GetDefinition(EventType.ApplicationCrash).Fields, field => field.Name == name);
            Assert.DoesNotContain(Assert.Single(report.Sections).Columns, column => column.Name == name);
        }
        Assert.Contains(Assert.Single(report.Sections).Columns, column => column.Name == "ApplicationName");
    }

    [Fact]
    public void Reports_PreserveNumericErrorSeverityWithoutEnglishResourceLabel() {
        DfsReplicationError record = Project<DfsReplicationError>("dfs-replication-error-5002", EventType.DfsReplicationError);
        EventReport report = EventReportEngine.Create(new object[] { record });
        EventReportRow row = Assert.Single(report.Rows);
        Assert.Equal((byte)2, row.LevelValue);
        Assert.Equal("Error", row.Level);
        row.Level = "Erreur";
        string html = EventReportHtmlRenderer.Render(report);
        Assert.Contains("data-hfx-monitoring-filter-values=\"state:critical ", html);
    }

    [Fact]
    public void ReportTimes_ShowTheCapturedInstantAndPreserveUnspecifiedTaskWallTime() {
        DfsReplicationError record = Project<DfsReplicationError>("dfs-replication-error-5002", EventType.DfsReplicationError);
        Assert.Equal("2026-11-01 12:00:00Z", EventReportPresentationProjection.FormatValue(record.When));
        ScheduledTaskCreated task = Project<ScheduledTaskCreated>("scheduled-task-created-4698", EventType.ScheduledTaskCreated);
        Assert.Equal(DateTimeKind.Unspecified, task.Created!.Value.Kind);
        Assert.Equal("2026-11-01 12:00:00", EventReportPresentationProjection.FormatValue(task.Created.Value));
    }
}