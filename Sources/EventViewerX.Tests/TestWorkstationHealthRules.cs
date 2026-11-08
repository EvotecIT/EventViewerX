using System.Xml.Linq;
using EventViewerX.Native;
using EventViewerX.Rules.Windows;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestWorkstationHealthRules {
    [Fact]
    public void BitLockerSuspended_SelectsManagementSuspensionEvent() {
        BitLockerSuspended record = Project<BitLockerSuspended>("bitlocker-suspended-773", EventType.BitLockerSuspended);
        Assert.Equal("workstation-fixture", record.Computer);
        Assert.Equal(773, record.EventId);
        Assert.Null(record.Volume); // The provider supplies a volume path, not a volume-type code.
        Assert.Empty(record.Who); // This payload does not identify the account that requested suspension.
        string query = EventDefinitionCompiler.BuildQueryXml(new[] { EventType.BitLockerSuspended });
        Assert.Contains("Microsoft-Windows-BitLocker/BitLocker Management", query);
        Assert.Contains("EventID=773", query);
        Assert.Contains("Provider[@Name='Microsoft-Windows-BitLocker-API']", query);
    }

    [Fact]
    public void WindowsUpdateFailure_SelectsSystemInstallFailureAndErrorCode() {
        WindowsUpdateFailure record = Project<WindowsUpdateFailure>("windows-update-failure-20", EventType.WindowsUpdateFailure);
        Assert.Equal("workstation-fixture", record.Computer);
        Assert.Equal("0x80073d02", record.Reason);
        Assert.Empty(record.KB); // Store app failures have no KB article.
        string query = EventDefinitionCompiler.BuildQueryXml(new[] { EventType.WindowsUpdateFailure });
        Assert.Contains("Path=\"System\"", query);
        Assert.Contains("EventID=20", query);
        Assert.Contains("Provider[@Name='Microsoft-Windows-WindowsUpdateClient']", query);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplicationCrash_ProjectsNamedAndClassicFaultDetails(bool unnamed) {
        ApplicationCrash record = Project<ApplicationCrash>("application-crash-1000", EventType.ApplicationCrash,
            unnamed ? RemoveDataNames : null);

        Assert.Equal("HealthFixture.exe", record.ApplicationName);
        Assert.Equal("1.2.3.4", record.ApplicationVersion);
        Assert.Equal("fixture.dll", record.ModuleName);
        Assert.Equal("2.3.4.5", record.ModuleVersion);
        Assert.Equal("c0000409", record.ExceptionCode);
        Assert.Equal("00000000001e4e40", record.FaultOffset);
        Assert.Equal(@"C:\Program Files\HealthFixture\HealthFixture.exe", record.ApplicationPath);
        Assert.Equal(@"C:\Program Files\HealthFixture\fixture.dll", record.ModulePath);
        Assert.Equal("11111111-2222-3333-4444-555555555555", record.ReportId);
    }

    [Theory]
    [InlineData(EventReadMode.RawXml)]
    [InlineData(EventReadMode.StructuredData)]
    public void ApplicationHang_ProjectsClassicIdentityAndHangType(EventReadMode mode) {
        ApplicationHang record = Assert.IsType<ApplicationHang>(EventTypeCatalog.CreateEventRule(
            Source("application-hang-1002", mode: mode), new[] { EventType.ApplicationHang }));

        Assert.Equal("FTK Imager.exe", record.ApplicationName);
        Assert.Equal("4.5.0.3", record.ApplicationVersion);
        Assert.Equal(@"C:\Program Files\AccessData\FTK Imager\FTK Imager.exe", record.ApplicationPath);
        Assert.Equal("Unknown", record.HangType);
        Assert.Equal("Value", record.ReportId);
    }

    [Theory]
    [InlineData("service-failure-7031", "Health Fixture Service", 28L, 180000L, "Restart the service")]
    [InlineData("service-failure-7034", "PsExec", 1, null, "")]
    public void ServiceFailure_ProjectsCrashCountAndOnlyAnnouncedRecovery(string fixture, string service,
        long count, long? recoveryDelay, string recovery) {
        ServiceFailure record = Project<ServiceFailure>(fixture, EventType.ServiceFailure);

        Assert.Equal(service, record.ServiceName);
        Assert.Equal("UnexpectedTermination", record.FailureKind);
        Assert.Equal(count, record.FailureCount);
        Assert.Equal(recoveryDelay, record.RecoveryDelayMilliseconds);
        Assert.Equal(recovery, record.RecoveryAction);
        Assert.Null(record.TimeoutMilliseconds);
        Assert.Empty(record.Error);
    }

    [Theory]
    [InlineData(7000, "Health Fixture Service", "%%1053", "StartFailure", null)]
    [InlineData(7009, "30000", "Health Fixture Service", "StartTimeout", 30000L)]
    [InlineData(7023, "Health Fixture Service", "%%5", "TerminationError", null)]
    public void ServiceFailure_UsesEventSpecificInsertionOrder(int id, string first, string second,
        string kind, long? timeout) {
        ServiceFailure record = Project<ServiceFailure>("service-failure-7031", EventType.ServiceFailure, xml => {
            SystemElement(xml, "EventID").Value = id.ToString();
            XElement data = xml.Elements().Single(element => element.Name.LocalName == "EventData");
            data.RemoveNodes();
            data.Add(new XElement(data.Name.Namespace + "Data", new XAttribute("Name", "param1"), first),
                new XElement(data.Name.Namespace + "Data", new XAttribute("Name", "param2"), second));
        });

        Assert.Equal("Health Fixture Service", record.ServiceName);
        Assert.Equal(kind, record.FailureKind);
        Assert.Equal(id == 7009 ? "" : second, record.Error);
        Assert.Equal(timeout, record.TimeoutMilliseconds);
        Assert.Null(record.FailureCount);
        Assert.Null(record.RecoveryDelayMilliseconds);
        Assert.Empty(record.RecoveryAction);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(51)]
    [InlineData(153)]
    public void DiskError_ProjectsDeviceAndRetrySpecificAddress(int id) {
        DiskError record = Project<DiskError>("disk-retry-153", EventType.DiskError,
            xml => SystemElement(xml, "EventID").Value = id.ToString());

        Assert.Equal(@"\Device\Harddisk1\DR1", record.DeviceName);
        Assert.Equal(id == 153 ? "600848" : "", record.LogicalBlockAddress);
        Assert.Equal(id == 153 ? 1L : null, record.DiskNumber);
    }

    [Fact]
    public void FileSystemError_ProjectsCorruptionAndRejectsHealthyVolume() {
        FileSystemError record = Project<FileSystemError>("ntfs-corruption-55", EventType.FileSystemError);
        Assert.Equal(@"\\?\Volume{11111111-2222-3333-4444-555555555555}", record.VolumeName);
        Assert.Equal(@"\Device\HarddiskVolume3", record.DeviceName);
        Assert.Contains("corrupted file record", record.Details);
        Assert.Equal("Read Only Volume", record.Outcome);
        Assert.Null(EventTypeCatalog.CreateEventRule(Source("ntfs-healthy-98"), new[] { EventType.FileSystemError }));

        FileSystemError needsAction = Project<FileSystemError>("ntfs-healthy-98", EventType.FileSystemError,
            xml => DataElement(xml, "CorruptionActionState").Value = "1");
        Assert.Equal(1L, needsAction.CorruptionActionState);
        Assert.Equal("TestVolume", needsAction.VolumeName);
    }

    [Fact]
    public void StorageReset_ProjectsPortWithoutInventingDiskIdentity() {
        StorageReset record = Project<StorageReset>("storage-reset-129", EventType.StorageReset);
        Assert.Equal(@"\Device\RaidPort1", record.DeviceName);
    }

    [Theory]
    [InlineData(17, "PCI Express", true, false)]
    [InlineData(18, "Processor Core", false, true)]
    [InlineData(19, "Processor Core", true, false)]
    [InlineData(47, "Memory", true, false)]
    public void HardwareError_DistinguishesCorrectedFromFatalSchemas(int id, string component, bool corrected, bool fatal) {
        HardwareError record = Project<HardwareError>("hardware-corrected-17", EventType.HardwareError,
            xml => {
                SystemElement(xml, "EventID").Value = id.ToString();
                if (id != 17) {
                    XElement data = xml.Elements().Single(element => element.Name.LocalName == "EventData");
                    data.RemoveNodes();
                    data.Add(new XElement(data.Name.Namespace + "Data", new XAttribute("Name", "ErrorSource"), "1"));
                    if (id == 47) {
                        data.Add(new XElement(data.Name.Namespace + "Data", new XAttribute("Name", "PhysicalAddress"), "0x12340000"));
                    } else {
                        data.Add(new XElement(data.Name.Namespace + "Data", new XAttribute("Name", "ApicId"), "9"),
                            new XElement(data.Name.Namespace + "Data", new XAttribute("Name", "ErrorType"), "9"));
                    }
                }
            });

        Assert.Equal(component, record.Component);
        Assert.Equal(corrected, record.IsCorrected);
        Assert.Equal(fatal, record.IsFatal);
        Assert.Equal(id == 17 ? "4" : "1", record.ErrorSource);
        if (id == 17) {
            Assert.Equal(@"PCI\VEN_8086&DEV_06B5&SUBSYS_1E911043&REV_F0", record.DeviceName);
        } else if (id == 47) {
            Assert.Equal("0x12340000", record.PhysicalAddress);
            Assert.Empty(record.ProcessorApicId);
        } else {
            Assert.Equal("9", record.ProcessorApicId);
            Assert.Equal("9", record.ErrorType);
            Assert.Empty(record.DeviceName);
        }
    }

    [Fact]
    public void DisplayDriverReset_ProjectsRecoveredDriver() {
        DisplayDriverReset record = Project<DisplayDriverReset>("display-reset-4101", EventType.DisplayDriverReset);
        Assert.Equal("nvlddmkm", record.DriverName);
    }

    [Fact]
    public void BootPerformance_ProjectsMillisecondsAndIncidentTime() {
        BootPerformance record = Project<BootPerformance>("boot-performance-100", EventType.BootPerformance);
        Assert.Equal(102026L, record.DurationMilliseconds);
        Assert.Equal(33326L, record.MainPathMilliseconds);
        Assert.Equal(68700L, record.PostBootMilliseconds);
        Assert.True(record.IsDegradation);
        Assert.Equal(DateTime.Parse("2020-10-09T02:53:52.9883590Z").ToUniversalTime(), record.IncidentTimestampUtc);
    }

    [Fact]
    public void ShutdownPerformance_ProjectsMillisecondsAndDegradation() {
        ShutdownPerformance record = Project<ShutdownPerformance>("shutdown-performance-200", EventType.ShutdownPerformance);
        Assert.Equal(31312L, record.DurationMilliseconds);
        Assert.Equal(4997L, record.ServicesMilliseconds);
        Assert.True(record.IsDegradation);
        Assert.Equal(DateTime.Parse("2020-10-09T17:00:48.2298106Z").ToUniversalTime(), record.IncidentTimestampUtc);
    }

    [Theory]
    [InlineData(101, "Application")]
    [InlineData(102, "Driver")]
    [InlineData(103, "Service")]
    [InlineData(104, "CoreSystem")]
    [InlineData(105, "ForegroundPrefetch")]
    [InlineData(106, "BackgroundPrefetch")]
    [InlineData(107, "MachinePolicy")]
    [InlineData(108, "UserPolicy")]
    [InlineData(109, "Device")]
    [InlineData(110, "SessionManager")]
    public void StartupDegradation_ProjectsNamedComponentAndTiming(int id, string component) {
        StartupDegradation record = Project<StartupDegradation>("startup-degradation-101", EventType.StartupDegradation,
            xml => {
                SystemElement(xml, "EventID").Value = id.ToString();
                if (id != 101 && id != 102 && id != 103 && id != 109) {
                    foreach (XElement element in xml.Descendants().Where(element =>
                        new[] { "FriendlyName", "Version", "Path", "ProductName", "CompanyName" }
                            .Contains((string?)element.Attribute("Name"))).ToArray()) {
                        element.Remove();
                    }
                }
            });
        Assert.Equal(component, record.Component);
        Assert.Equal("svchost.exe", record.Name);
        Assert.Equal(3865L, record.DurationMilliseconds);
        Assert.Equal(1341L, record.DegradationMilliseconds);
        Assert.Equal(id is 101 or 102 or 103 or 109 ? @"C:\Windows\System32\svchost.exe" : "", record.Path);
        Assert.Equal(DateTime.Parse("2022-06-16T15:01:58.9212636Z").ToUniversalTime(), record.IncidentTimestampUtc);
    }

    [Fact]
    public void ShutdownDegradation_ProjectsServiceAndAdditionalTime() {
        ShutdownDegradation record = Project<ShutdownDegradation>("shutdown-degradation-203", EventType.ShutdownDegradation);
        Assert.Equal("Service", record.Component);
        Assert.Equal("Dhcp", record.Name);
        Assert.Equal("Servicio cliente DHCP", record.FriendlyName);
        Assert.Equal(@"C:\Windows\system32\dhcpcore.dll", record.Path);
        Assert.Equal(4967L, record.DurationMilliseconds);
        Assert.Equal(967L, record.DegradationMilliseconds);
    }

    [Theory]
    [InlineData("application-crash-1000", EventType.ApplicationCrash)]
    [InlineData("bitlocker-suspended-773", EventType.BitLockerSuspended)]
    [InlineData("windows-update-failure-20", EventType.WindowsUpdateFailure)]
    [InlineData("application-hang-1002", EventType.ApplicationHang)]
    [InlineData("service-failure-7031", EventType.ServiceFailure)]
    [InlineData("disk-retry-153", EventType.DiskError)]
    [InlineData("ntfs-corruption-55", EventType.FileSystemError)]
    [InlineData("storage-reset-129", EventType.StorageReset)]
    [InlineData("hardware-corrected-17", EventType.HardwareError)]
    [InlineData("display-reset-4101", EventType.DisplayDriverReset)]
    [InlineData("boot-performance-100", EventType.BootPerformance)]
    [InlineData("shutdown-performance-200", EventType.ShutdownPerformance)]
    [InlineData("startup-degradation-101", EventType.StartupDegradation)]
    [InlineData("shutdown-degradation-203", EventType.ShutdownDegradation)]
    public void HealthRules_RejectUnrelatedProvidersAndChannels(string fixture, EventType type) {
        Assert.NotNull(EventTypeCatalog.CreateEventRule(Source(fixture), new[] { type }));
        Assert.Null(EventTypeCatalog.CreateEventRule(Source(fixture,
            xml => SystemElement(xml, "Provider").SetAttributeValue("Name", "Unrelated.Provider")), new[] { type }));
        Assert.Null(EventTypeCatalog.CreateEventRule(Source(fixture,
            xml => SystemElement(xml, "Channel").Value = "Unrelated/Operational"), new[] { type }));
    }

    private static T Project<T>(string fixture, EventType type, Action<XElement>? mutate = null) =>
        Assert.IsType<T>(EventTypeCatalog.CreateEventRule(Source(fixture, mutate), new[] { type }));

    private static EventObject Source(string fixture, Action<XElement>? mutate = null, EventReadMode mode = EventReadMode.RawXml) {
        using Stream stream = typeof(TestWorkstationHealthRules).Assembly.GetManifestResourceStream(
            "WorkstationHealth." + fixture + ".xml")!;
        XElement xml = XElement.Load(stream);
        mutate?.Invoke(xml);
        SavedEventRecord saved = SavedEventXmlProjector.Create(xml.ToString());
        if (mode == EventReadMode.RawXml) {
            // Native raw reads parse their XML lazily without a saved-event dictionary overlay.
            var metadata = new NativeEventMetadata(saved.ProviderName, saved.ProviderId, saved.EventId,
                saved.Qualifiers, saved.Level, saved.Task, saved.Opcode, saved.Keywords,
                saved.TimeCreatedUtc.ToLocalTime(), saved.RecordId, saved.ActivityId, saved.RelatedActivityId,
                saved.ProcessId, saved.ThreadId, saved.Channel, saved.Computer, userId: null, version: saved.Version);
            return new EventObject(metadata, xml.ToString(), bookmark: null,
                queriedMachine: "health-fixture", containerLog: saved.Channel);
        }
        // Exercise the parser-neutral dictionary without repairing it from the raw XML.
        saved.RawXml = string.Empty;
        return saved.ToEventObject("health-fixture", mode);
    }

    private static XElement SystemElement(XElement xml, string name) => xml.Elements()
        .Single(element => element.Name.LocalName == "System").Elements().Single(element => element.Name.LocalName == name);

    private static XElement DataElement(XElement xml, string name) => xml.Descendants()
        .Single(element => element.Name.LocalName == "Data" && (string?)element.Attribute("Name") == name);

    private static void RemoveDataNames(XElement xml) {
        foreach (XElement element in xml.Descendants().Where(element => element.Name.LocalName == "Data")) {
            element.Attribute("Name")?.Remove();
        }
    }
}
