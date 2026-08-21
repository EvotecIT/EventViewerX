using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Security.Principal;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventObjectMessageParsing
{
    [Fact]
    public void Constructor_WhenFormatDescriptionThrows_MessageIsEmptyAndNoException()
    {
        var record = new MessageRecord(formatDescriptionException: new EventLogException("format failed"));

        var eo = new EventObject(record, "localhost");

        Assert.Equal(string.Empty, eo.Message);
        Assert.Empty(eo.MessageLines);
        Assert.Empty(eo.MessageData);
    }

    [Fact]
    public void Constructor_WhenToXmlThrows_XmlAndDataFallbackToEmpty()
    {
        var record = new MessageRecord(xmlException: new EventLogException("xml failed"));

        var eo = new EventObject(record, "localhost");

        Assert.Equal(string.Empty, eo.XMLData);
        Assert.Empty(eo.Data);
    }

    [Fact]
    public void ParseMessage_UsesFirstColonOnlyAndCaseInsensitiveKeys()
    {
        var message = "Subject line\r\nKey: value:with:colon\r\nAnother: entry";
        var record = new MessageRecord(message: message);

        var eo = new EventObject(record, "localhost");

        Assert.Equal("Subject line", eo.MessageSubject);
        Assert.Equal("value:with:colon", eo.MessageData["key"]);
        Assert.Equal("entry", eo.MessageData["ANOTHER"]);
    }

    [Fact]
    public void TryGetMessageLine_SupportsNegativeIndexesAndTrimming()
    {
        var message = "first\r\n second \r\nthird";
        var record = new MessageRecord(message: message);
        var eo = new EventObject(record, "localhost");

        Assert.True(eo.TryGetMessageLine(1, out var second));
        Assert.Equal("second", second);

        Assert.True(eo.TryGetMessageLine(-1, out var last));
        Assert.Equal("third", last);

        Assert.False(eo.TryGetMessageLine(99, out _));
        Assert.Equal(string.Empty, eo.GetMessageLineOrEmpty(99));
    }

    private sealed class MessageRecord : EventRecord
    {
        private readonly string _message;
        private readonly string _xml;
        private readonly Exception? _formatDescriptionException;
        private readonly Exception? _xmlException;

        public MessageRecord(
            string message = "",
            string xml = "<Event></Event>",
            Exception? formatDescriptionException = null,
            Exception? xmlException = null)
        {
            _message = message;
            _xml = xml;
            _formatDescriptionException = formatDescriptionException;
            _xmlException = xmlException;
        }

        public override string ProviderName => "TestProvider";
        public override string LogName => "Application";
        public override string MachineName => Environment.MachineName;
        public override int Id => 1946;
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

        public override string FormatDescription()
        {
            if (_formatDescriptionException != null)
            {
                throw _formatDescriptionException;
            }

            return _message;
        }

        public override string FormatDescription(IEnumerable<object> values)
        {
            return FormatDescription();
        }

        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => DateTime.UtcNow;
        public override int? Qualifiers => null;
        public override long? RecordId => 1;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;

        protected override void Dispose(bool disposing)
        {
        }

        public override string ToXml()
        {
            if (_xmlException != null)
            {
                throw _xmlException;
            }

            return _xml;
        }
    }
}
