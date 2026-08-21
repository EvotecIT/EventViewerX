using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Principal;
using System.Reflection;
using System.Runtime.Serialization;
using System.Diagnostics.Eventing.Reader;
using Xunit;

namespace EventViewerX.Tests;

public class TestRuleHelpers
{
    [Fact]
    public void ParseUnlabeledOsTimestamp_ParsesDateAndTime()
    {
        var data = new Dictionary<string, string>
        {
            { "NoNameA1", "2025-01-02" },
            { "NoNameA0", "13:45:00" }
        };

        var eo = BuildEventObject(message: string.Empty, provider: "TestProvider", containerLog: "System", data: data);

        var actual = Rules.RuleHelpers.ParseUnlabeledOsTimestamp(eo);
        Assert.True(actual.HasValue);

        var expectedLocal = DateTime.Parse("2025-01-02 13:45:00", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
        Assert.Equal(expectedLocal.ToUniversalTime(), actual.Value);
    }

    [Fact]
    public void IsProvider_IsCaseInsensitive()
    {
        var eo = BuildEventObject(message: string.Empty, provider: "Microsoft-Windows-Eventlog", containerLog: "System");
        Assert.True(Rules.RuleHelpers.IsProvider(eo, "microsoft-windows-eventlog"));
        Assert.False(Rules.RuleHelpers.IsProvider(eo, "OtherProvider"));
    }

    [Fact]
    public void IsChannel_IsCaseInsensitive()
    {
        var eo = BuildEventObject(message: string.Empty, provider: "Test", containerLog: "Application");
        Assert.True(Rules.RuleHelpers.IsChannel(eo, "application"));
        Assert.False(Rules.RuleHelpers.IsChannel(eo, "System"));
    }

    [Fact]
    public void IsChannel_UsesOriginalChannelForForwardedEvents()
    {
        var eo = BuildEventObject(
            message: string.Empty,
            provider: "Test",
            containerLog: "ForwardedEvents",
            originalLog: "Security");

        Assert.True(Rules.RuleHelpers.IsChannel(eo, "security"));
        Assert.False(Rules.RuleHelpers.IsChannel(eo, "ForwardedEvents"));
        Assert.Equal("Security", eo.OriginalLogName);
        Assert.Equal("ForwardedEvents", eo.ContainerLogName);
    }

    [Fact]
    public void GetMessage_PrefersLongestAvailable()
    {
        var data = new Dictionary<string, string> { { "NoNameA0", "DataFallback" } };
        var eo = BuildEventObject(message: "Short", messageSubject: "SubjectIsLonger", provider: "P", containerLog: "C", data: data);
        Assert.Equal("SubjectIsLonger", Rules.RuleHelpers.GetMessage(eo));
    }

    [Fact]
    public void GetMessage_FallsBackToData()
    {
        var data = new Dictionary<string, string> { { "NoNameA0", "FromData" } };
        var eo = BuildEventObject(message: string.Empty, messageSubject: string.Empty, provider: "P", containerLog: "C", data: data);
        Assert.Equal("FromData", Rules.RuleHelpers.GetMessage(eo));
    }

    private static EventObject BuildEventObject(string message, string provider, string containerLog, Dictionary<string, string>? data = null, string messageSubject = "", string? originalLog = null)
    {
        var record = (FakeEventRecord)FormatterServices.GetUninitializedObject(typeof(FakeEventRecord));
        SetField(record, "_provider", provider);
        SetField(record, "_log", originalLog ?? containerLog);
        SetField(record, "_message", message);

        var eo = new EventObject(record, "local", EventReadMode.Message);
        eo.MessageSubject = messageSubject;
        eo.ContainerLog = containerLog;
        eo.XMLData = string.Empty;
        eo.GatheredFrom = "local";
        eo.GatheredLogName = containerLog;
        SetProperty(eo, nameof(EventObject.Data), data ?? new Dictionary<string, string>());
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
        private readonly string _provider;
        private readonly string _log;
        private readonly string _message;

        public FakeEventRecord(string provider, string log, string message)
        {
            _provider = provider;
            _log = log;
            _message = message;
        }

        public override string ProviderName => _provider;
        public override string LogName => _log;
        public override string MachineName => Environment.MachineName;
        public override int Id => 0;
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
        public override string FormatDescription() => _message;
        public override string FormatDescription(IEnumerable<object> values) => _message;
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => DateTime.UtcNow;
        public override int? Qualifiers => null;
        public override long? RecordId => 0;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null;
        public override EventBookmark Bookmark => null;
        protected override void Dispose(bool disposing) { }
        public override string ToXml() => "<Event></Event>";
    }
}
