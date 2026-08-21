using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Principal;
using EventViewerX.Rules.ActiveDirectory;
using EventViewerX.Rules.DHCP;
using EventViewerX.Rules.Logging;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventObjectTypedAccessors
{
    [Fact]
    public void TryGetDataValue_KnownField_IsCaseInsensitive()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ipaddress"] = "10.20.30.40"
        });

        Assert.True(eo.TryGetDataValue(KnownEventField.IpAddress, out var ipAddress));
        Assert.Equal("10.20.30.40", ipAddress);
    }

    [Fact]
    public void TryGetDataValue_KnownField_CustomMapping_IsCaseInsensitive()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["clientip"] = "172.16.10.25",
            ["hwaddress"] = "AA-BB-CC-DD-EE-11",
            ["privilegelist"] = "SeBackupPrivilege SeRestorePrivilege"
        });

        Assert.True(eo.TryGetDataValue(KnownEventField.ClientIp, out var clientIp));
        Assert.Equal("172.16.10.25", clientIp);

        Assert.True(eo.TryGetDataValue(KnownEventField.HwAddress, out var hwAddress));
        Assert.Equal("AA-BB-CC-DD-EE-11", hwAddress);

        Assert.True(eo.TryGetDataValue(KnownEventField.PrivilegeList, out var privilegeList));
        Assert.Equal("SeBackupPrivilege SeRestorePrivilege", privilegeList);
    }

    [Fact]
    public void TryGetDataEnum_ParsesHexAndPrefixedValues()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>
        {
            ["Status"] = "0xC000006D",
            ["SubStatus"] = "0xC0000072",
            ["FailureReason"] = "%%2304",
            ["LogonType"] = "3"
        });

        Assert.True(eo.TryGetDataEnum(KnownEventField.Status, out StatusCode status, EventFieldNumericBase.Hexadecimal));
        Assert.Equal(StatusCode.StatusLogonFailure, status);

        Assert.True(eo.TryGetDataEnum(KnownEventField.SubStatus, out SubStatusCode subStatus, EventFieldNumericBase.Hexadecimal));
        Assert.Equal(SubStatusCode.StatusAccountDisabled, subStatus);

        Assert.True(eo.TryGetDataEnum(KnownEventField.FailureReason, out FailureReason failureReason, EventFieldNumericBase.Decimal, "%%"));
        Assert.Equal(FailureReason.UnknownUserNameOrBadPassword, failureReason);

        Assert.True(eo.TryGetDataEnum(KnownEventField.LogonType, out LogonType logonType, EventFieldNumericBase.Decimal));
        Assert.Equal(LogonType.Network, logonType);
    }

    [Fact]
    public void TryGetMessageValue_TextPayload_UsesSpecialKeyMapping()
    {
        var eo = BuildEventObject(messageData: new Dictionary<string, string>
        {
            ["#text"] = "payload from xml text node"
        });

        Assert.True(eo.TryGetMessageValue(KnownEventField.TextPayload, out var payload));
        Assert.Equal("payload from xml text node", payload);
    }

    [Fact]
    public void ADUserLogonFailed_ParsesTypedEnumFields()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>
        {
            ["WorkstationName"] = "CLIENT01",
            ["TargetUserName"] = "john.smith",
            ["TargetDomainName"] = "contoso",
            ["IpAddress"] = "192.168.0.11",
            ["IpPort"] = "50001",
            ["LogonProcessName"] = "NtLmSsp",
            ["LogonType"] = "3",
            ["Status"] = "0xC000006D",
            ["SubStatus"] = "0xC0000072",
            ["FailureReason"] = "%%2304",
            ["LmPackageName"] = "NTLM V2",
            ["KeyLength"] = "128",
            ["ProcessId"] = "0x3e7",
            ["ProcessName"] = "C:\\Windows\\System32\\lsass.exe",
            ["TransmittedServices"] = "-",
            ["AuthenticationPackageName"] = "NTLM"
        });

        var rule = new ADUserLogonFailed(eo);

        Assert.Equal(LogonType.Network, rule.LogonType);
        Assert.Equal(StatusCode.StatusLogonFailure, rule.Status);
        Assert.Equal(SubStatusCode.StatusAccountDisabled, rule.SubStatus);
        Assert.Equal(FailureReason.UnknownUserNameOrBadPassword, rule.FailureReason);
        Assert.Equal("NTLM", rule.PackageName);
        Assert.Equal("CLIENT01", rule.Who);
    }

    [Fact]
    public void ADUserLogon_ParsesTypedLogonType()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["logontype"] = "3",
            ["subjectusername"] = "svc.account",
            ["subjectdomainname"] = "contoso"
        });

        var rule = new ADUserLogon(eo);

        Assert.Equal(LogonType.Network, rule.LogonType);
        Assert.Equal("contoso\\svc.account", rule.Who);
    }

    [Fact]
    public void ADUserLogonNTLMv1_ParsesKnownFieldsCaseInsensitive()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["lmPackageName"] = "NTLM V1",
            ["ipaddress"] = "10.10.10.50",
            ["ipport"] = "61542",
            ["logonprocessname"] = "NtLmSsp",
            ["logontype"] = "3",
            ["targetusername"] = "john.smith",
            ["targetdomainname"] = "contoso",
            ["subjectusername"] = "svc.account",
            ["subjectdomainname"] = "contoso",
            ["authenticationpackagename"] = "NTLM",
            ["keylength"] = "128",
            ["processid"] = "0x3e7",
            ["processname"] = "C:\\Windows\\System32\\lsass.exe"
        });

        var rule = new ADUserLogonNTLMv1(eo);

        Assert.Equal("10.10.10.50", rule.IpAddress);
        Assert.Equal("61542", rule.IpPort);
        Assert.Equal(LogonType.Network, rule.LogonType);
        Assert.Equal("contoso\\john.smith", rule.ObjectAffected);
        Assert.Equal("contoso\\svc.account", rule.Who);
        Assert.Equal("NTLM V1", rule.LmPackageName);
        Assert.Equal("NTLM", rule.PackageName);
    }

    [Fact]
    public void ADUserUnlocked_UsesKnownFieldTargetDomainName()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["targetdomainname"] = "CONTOSO-DC",
            ["targetusername"] = "john.smith",
            ["subjectusername"] = "admin",
            ["subjectdomainname"] = "contoso"
        });

        var rule = new ADUserUnlocked(eo);

        Assert.Equal("CONTOSO-DC", rule.ComputerLockoutOn);
        Assert.Equal("CONTOSO-DC\\john.smith", rule.UserAffected);
        Assert.Equal("contoso\\admin", rule.Who);
    }

    [Fact]
    public void LogsClearedSecurity_MissingChannel_DoesNotThrowAndFallsBack()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>
        {
            ["SubjectUserName"] = "admin",
            ["SubjectDomainName"] = "contoso"
        });

        Exception? exception = Record.Exception(() => _ = new LogsClearedSecurity(eo));

        Assert.Null(exception);

        var rule = new LogsClearedSecurity(eo);
        Assert.Equal("Unknown Operation", rule.LogType);
        Assert.Equal("contoso\\admin", rule.Who);
    }

    [Fact]
    public void GetValueFromDataDictionary_IsCaseInsensitive_AndSupportsReverseOrder()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["subjectusername"] = "svc.account",
            ["subjectdomainname"] = "contoso"
        });

        var combined = eo.GetValueFromDataDictionary("SubjectUserName", "SubjectDomainName", "\\", reverseOrder: true);

        Assert.Equal("contoso\\svc.account", combined);
    }

    [Fact]
    public void GetValueFromDataDictionary_KnownFieldOverload_UsesCanonicalKeys()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["targetusername"] = "john.smith",
            ["targetdomainname"] = "contoso"
        });

        var combined = eo.GetValueFromDataDictionary(KnownEventField.TargetUserName, KnownEventField.TargetDomainName, "\\", reverseOrder: true);

        Assert.Equal("contoso\\john.smith", combined);
    }

    [Fact]
    public void GetValueFromDataDictionary_MixedKnownFieldString_UsesFallbackKey()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["clientip"] = "172.16.1.15"
        });

        var value = eo.GetValueFromDataDictionary(KnownEventField.IpAddress, "ClientIP");

        Assert.Equal("172.16.1.15", value);
    }

    [Fact]
    public void GetSubjectAccountOrEmpty_UsesKnownSubjectFieldsCaseInsensitive()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["subjectusername"] = "svc.account",
            ["subjectdomainname"] = "contoso"
        });

        var value = eo.GetSubjectAccountOrEmpty();

        Assert.Equal("contoso\\svc.account", value);
    }

    [Fact]
    public void GetTargetAccountOrEmpty_UsesKnownTargetFieldsCaseInsensitive()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["targetusername"] = "john.smith",
            ["targetdomainname"] = "contoso"
        });

        var value = eo.GetTargetAccountOrEmpty();

        Assert.Equal("contoso\\john.smith", value);
    }

    [Fact]
    public void ADComputerChangeDetailed_CanHandle_IsCaseInsensitiveForObjectClass()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["objectclass"] = "COMPUTER"
        });

        var rule = new ADComputerChangeDetailed(eo);

        Assert.True(rule.CanHandle(eo));
    }

    [Fact]
    public void DhcpLeaseCreated_UsesKnownFieldOrFallbackClientIp()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["clientip"] = "10.20.30.40",
            ["macaddress"] = "AA-BB-CC-DD-EE-FF"
        });

        var rule = new DhcpLeaseCreated(eo);

        Assert.Equal("10.20.30.40", rule.IPAddress);
        Assert.Equal("AA-BB-CC-DD-EE-FF", rule.MacAddress);
    }

    [Fact]
    public void DhcpLeaseCreated_WhenHwAndMacPresent_UsesCombinedValue()
    {
        var eo = BuildEventObject(data: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ipaddress"] = "10.20.30.41",
            ["hwaddress"] = "AA-BB-CC-DD-EE-01",
            ["macaddress"] = "AA-BB-CC-DD-EE-FF"
        });

        var rule = new DhcpLeaseCreated(eo);

        Assert.Equal("10.20.30.41", rule.IPAddress);
        Assert.Equal("AA-BB-CC-DD-EE-01\\AA-BB-CC-DD-EE-FF", rule.MacAddress);
    }

    private static EventObject BuildEventObject(
        Dictionary<string, string>? data = null,
        Dictionary<string, string>? messageData = null)
    {
        var record = (FakeEventRecord)FormatterServices.GetUninitializedObject(typeof(FakeEventRecord));
        SetField(record, "_provider", "Microsoft-Windows-Security-Auditing");
        SetField(record, "_log", "Security");
        SetField(record, "_id", 4625);

        var eo = new EventObject(record, "testhost", EventReadMode.Metadata);
        eo.MessageSubject = "An account failed to log on.";
        eo.ContainerLog = "Security";
        eo.XMLData = "<Event></Event>";
        eo.GatheredFrom = "testhost";
        eo.GatheredLogName = "Security";
        SetProperty(eo, nameof(EventObject.MessageLines), Array.Empty<string>());
        SetProperty(eo, nameof(EventObject.Data), data ?? new Dictionary<string, string>());
        SetProperty(eo, nameof(EventObject.MessageData), messageData ?? new Dictionary<string, string>());
        SetProperty(eo, nameof(EventObject.Attachments), Array.Empty<byte[]>());
        SetProperty(eo, nameof(EventObject.NicIdentifiers), new List<string>());
        return eo;
    }

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        f!.SetValue(target, value);
    }

    private static void SetProperty(object target, string name, object value)
    {
        var p = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        p!.SetValue(target, value);
    }

    private sealed class FakeEventRecord : EventRecord
    {
        private readonly int _id;
        private readonly string _log;
        private readonly string _provider;

        public FakeEventRecord(int id, string log, string provider)
        {
            _id = id;
            _log = log;
            _provider = provider;
        }

        public override string ProviderName => _provider;
        public override string LogName => _log;
        public override string MachineName => Environment.MachineName;
        public override int Id => _id;
        public override byte? Level => 4;
        public override int? Task => null;
        public override long? Keywords => null;
        public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();
        public override short? Opcode => 0;
        public override string OpcodeDisplayName => string.Empty;
        public override string TaskDisplayName => string.Empty;
        public override Guid? ProviderId => null;
        public override Guid? ActivityId => null;
        public override Guid? RelatedActivityId => null;
        public override int? ProcessId => 0;
        public override int? ThreadId => 0;
        public override string LevelDisplayName => "Information";
        public override string FormatDescription() => string.Empty;
        public override string FormatDescription(IEnumerable<object> values) => string.Empty;
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => DateTime.UtcNow;
        public override int? Qualifiers => null;
        public override long? RecordId => 1;
        public override byte? Version => 2;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;

        protected override void Dispose(bool disposing)
        {
        }

        public override string ToXml() => "<Event></Event>";
    }
}
