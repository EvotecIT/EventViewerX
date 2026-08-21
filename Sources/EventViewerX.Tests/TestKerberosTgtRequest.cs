using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Diagnostics.Eventing.Reader;
using Xunit;

namespace EventViewerX.Tests;

public class TestKerberosTgtRequest
{
    [Fact]
    public void Kerberos4768_ParsesEncryptionAndStatusFields()
    {
        var data = new Dictionary<string, string>
        {
            { "TargetUserName", "MSOL_6f0d1d4965ec" },
            { "TargetDomainName", "ad.evotec.xyz" },
            { "TicketOptions", "0x40810010" },
            { "Status", "0x0" },
            { "TicketEncryptionType", "0x12" },
            { "PreAuthType", "2" },
            { "IpAddress", "::ffff:192.168.241.15" },
            { "IpPort", "52295" },
            { "ClientAdvertizedEncryptionTypes", "AES256 AES128 RC4" },
            { "AccountSupportedEncryptionTypes", "0x27" },
            { "AccountAvailableKeys", "AES-SHA1, RC4" },
            { "ServiceSupportedEncryptionTypes", "0x1F" },
            { "ServiceAvailableKeys", "AES-SHA1, RC4" },
            { "DCSupportedEncryptionTypes", "0x1F" },
            { "DCAvailableKeys", "AES-SHA1, RC4" },
            { "SessionKeyEncryptionType", "0x12" },
            { "PreAuthEncryptionType", "0x12" },
            { "ResponseTicket", "ABC==" }
        };

        var eo = BuildEventObject(4768, "Security", "Microsoft-Windows-Security-Auditing", data);
        var rule = new Rules.Kerberos.KerberosTGTRequest(eo);

        Assert.Equal("MSOL_6f0d1d4965ec", rule.AccountName.Split('\\')[1]);
        Assert.Equal("192.168.241.15", rule.IpAddress);
        Assert.Contains("AES256", rule.EncryptionTypeText);
        Assert.Contains("0x00000000", rule.StatusText);
        Assert.Equal("AES-SHA1, RC4", rule.AccountAvailableKeys);
        Assert.Equal("AES256 AES128 RC4", rule.ClientAdvertizedEncryptionTypes);
        Assert.Equal("ABC==", rule.ResponseTicket);
        Assert.Contains("AES256", rule.SessionKeyEncryptionTypeText);
        Assert.Contains("AES256", rule.PreAuthEncryptionTypeText);
        Assert.Contains("Forwardable", rule.TicketOptionsText);
        Assert.Contains("Renewable", rule.TicketOptionsText);
        Assert.Contains("0x40810010", rule.TicketOptionsText);
        Assert.Contains("Password", rule.PreAuthTypeText);
    }

    private static EventObject BuildEventObject(int id, string log, string provider, Dictionary<string, string> data)
    {
        var record = (FakeEventRecord)FormatterServices.GetUninitializedObject(typeof(FakeEventRecord));
        SetField(record, "_provider", provider);
        SetField(record, "_log", log);
        SetField(record, "_id", id);

        var eo = new EventObject(record, "testhost", EventReadMode.Metadata);
        eo.MessageSubject = "A Kerberos authentication ticket (TGT) was requested.";
        eo.ContainerLog = log;
        eo.XMLData = "<Event></Event>";
        eo.GatheredFrom = "testhost";
        eo.GatheredLogName = log;
        SetProperty(eo, nameof(EventObject.Data), data);
        SetProperty(eo, nameof(EventObject.MessageData), new Dictionary<string, string>());
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
        public override SecurityIdentifier UserId => null;
        public override EventBookmark Bookmark => null;
        protected override void Dispose(bool disposing) { }
        public override string ToXml() => "<Event></Event>";
    }
}
