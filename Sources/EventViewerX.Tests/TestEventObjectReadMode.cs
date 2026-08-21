using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Security.Principal;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventObjectReadMode {
    [Fact]
    public void MetadataModeSnapshotsRecordAndDisposesNativeOwner() {
        var record = new TrackingEventRecord();

        var snapshot = new EventObject(record, "testhost", EventReadMode.Metadata);

        Assert.True(record.Disposed);
        Assert.Equal(0, record.FormatDescriptionCalls);
        Assert.Equal(0, record.ToXmlCalls);
        Assert.Equal(0, record.BookmarkCalls);
        Assert.Equal(42, snapshot.Id);
        Assert.Equal(1234, snapshot.RecordId);
        Assert.Equal("System", snapshot.LogName);
        Assert.Equal("testhost", snapshot.GatheredFrom);
        Assert.Empty(snapshot.Message);
        Assert.Empty(snapshot.XMLData);
    }

    [Fact]
    public void MetadataModeDefersOptionalMutableCollectionsUntilRequested() {
        var snapshot = new EventObject(new TrackingEventRecord(), "testhost", EventReadMode.Metadata);
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        Assert.Null(typeof(EventObject).GetField("_data", Flags)!.GetValue(snapshot));
        Assert.Null(typeof(EventObject).GetField("_messageData", Flags)!.GetValue(snapshot));
        Assert.Null(typeof(EventObject).GetField("_nicIdentifiers", Flags)!.GetValue(snapshot));

        snapshot.Data["Field"] = "Value";
        snapshot.MessageData["Message"] = "Subject";
        snapshot.NicIdentifiers.Add("nic");

        Assert.Equal("Value", snapshot.Data["field"]);
        Assert.Equal("Subject", snapshot.MessageData["message"]);
        Assert.Equal("nic", Assert.Single(snapshot.NicIdentifiers));
    }

    [Fact]
    public void DeferredCollectionsAreNotSharedBetweenSnapshots() {
        var first = new EventObject(new TrackingEventRecord(), "testhost", EventReadMode.Metadata);
        var second = new EventObject(new TrackingEventRecord(), "testhost", EventReadMode.Metadata);

        first.Data["Field"] = "Value";
        first.MessageData["Message"] = "Subject";
        first.NicIdentifiers.Add("nic");

        Assert.Empty(second.Data);
        Assert.Empty(second.MessageData);
        Assert.Empty(second.NicIdentifiers);
    }

    [Fact]
    public void MessageModeDoesNotReadOrParseXml() {
        var record = new TrackingEventRecord();

        var snapshot = new EventObject(record, "testhost", EventReadMode.Message);

        Assert.True(record.Disposed);
        Assert.Equal(1, record.FormatDescriptionCalls);
        Assert.Equal(0, record.ToXmlCalls);
        Assert.Equal("Subject", snapshot.MessageSubject);
        Assert.Equal("Value", snapshot.MessageData["Key"]);
        Assert.Empty(snapshot.Data);
    }

    [Fact]
    public void MessageModeDefersKeyValueProjectionUntilRequested() {
        var snapshot = new EventObject(new TrackingEventRecord(), "testhost", EventReadMode.Message);
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        Assert.Equal("Subject", snapshot.MessageSubject);
        Assert.Null(typeof(EventObject).GetField("_messageData", Flags)!.GetValue(snapshot));
        Assert.Null(typeof(EventObject).GetField("_messageLines", Flags)!.GetValue(snapshot));

        snapshot.MessageSubject = "Override";
        Assert.Equal("Value", snapshot.MessageData["Key"]);
        Assert.Equal("Override", snapshot.MessageSubject);
        Assert.NotNull(typeof(EventObject).GetField("_messageData", Flags)!.GetValue(snapshot));
        Assert.Null(typeof(EventObject).GetField("_messageLines", Flags)!.GetValue(snapshot));
    }

    [Fact]
    public void StructuredDataModeDefersPayloadParsingAndDoesNotFormatMessage() {
        var record = new TrackingEventRecord();

        var snapshot = new EventObject(record, "testhost", EventReadMode.StructuredData);
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        Assert.True(record.Disposed);
        Assert.Equal(0, record.FormatDescriptionCalls);
        Assert.Equal(1, record.ToXmlCalls);
        Assert.Null(typeof(EventObject).GetField("_data", Flags)!.GetValue(snapshot));
        Assert.Null(typeof(EventObject).GetField("_attachments", Flags)!.GetValue(snapshot));
        Assert.False((bool)typeof(EventObject).GetField("_payloadParsed", Flags)!.GetValue(snapshot)!);

        Assert.Equal("StructuredValue", snapshot.Data["Field"]);
        Assert.True((bool)typeof(EventObject).GetField("_payloadParsed", Flags)!.GetValue(snapshot)!);
        Assert.Empty(snapshot.Message);
        Assert.Empty(snapshot.Attachments);
    }

    [Fact]
    public void RawXmlModeSkipsMessageAndTypedPropertyProjection() {
        var record = new TrackingEventRecord();

        var snapshot =
            new EventObject(
                record,
                "testhost",
                EventReadMode.RawXml);

        Assert.True(record.Disposed);
        Assert.Equal(0, record.FormatDescriptionCalls);
        Assert.Equal(1, record.ToXmlCalls);
        Assert.Equal(0, record.BookmarkCalls);
        Assert.Empty(snapshot.Properties);
        Assert.Contains("StructuredValue", snapshot.XMLData);
        Assert.Equal(
            "StructuredValue",
            snapshot.Data["Field"]);
    }

    [Fact]
    public void RawXmlModeMaterializesBookmarkOnlyWhenRequested() {
        var record = new TrackingEventRecord();

        var snapshot =
            new EventObject(
                record,
                "testhost",
                EventReadMode.RawXml,
                includeBookmark: true);

        Assert.True(record.Disposed);
        Assert.Equal(1, record.BookmarkCalls);
        Assert.Null(snapshot.Bookmark);
    }

    [Fact]
    public void FullModeDecodesBinaryAttachments() {
        var record = new TrackingEventRecord();

        var snapshot = new EventObject(record, "testhost", EventReadMode.Full);

        byte[] attachment = Assert.Single(snapshot.Attachments);
        Assert.Equal(new byte[] { 1, 2, 255 }, attachment);
    }

    [Fact]
    public void StructuredDataAndMessageModePreservesTypedInputsWithoutDecodingAttachments() {
        var record = new TrackingEventRecord();

        var snapshot = new EventObject(
            record,
            "testhost",
            EventReadMode.StructuredDataAndMessage);

        Assert.True(record.Disposed);
        Assert.Equal(1, record.FormatDescriptionCalls);
        Assert.Equal(1, record.ToXmlCalls);
        Assert.Equal("Subject", snapshot.MessageSubject);
        Assert.Equal("Value", snapshot.MessageData["Key"]);
        Assert.Equal("StructuredValue", snapshot.Data["Field"]);
        Assert.Empty(snapshot.Attachments);
        Assert.Equal(EventReadMode.StructuredDataAndMessage, snapshot.ReadMode);
    }

    private sealed class TrackingEventRecord : EventRecord {
        internal bool Disposed { get; private set; }
        internal int FormatDescriptionCalls { get; private set; }
        internal int ToXmlCalls { get; private set; }
        internal int BookmarkCalls { get; private set; }

        public override string ProviderName => "TestProvider";
        public override string LogName => "System";
        public override string MachineName => "sourcehost";
        public override int Id => 42;
        public override byte? Level => 4;
        public override int? Task => 1;
        public override long? Keywords => 2;
        public override IEnumerable<string> KeywordsDisplayNames => new[] { "Keyword" };
        public override short? Opcode => 3;
        public override string OpcodeDisplayName => "Opcode";
        public override string TaskDisplayName => "Task";
        public override Guid? ProviderId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public override Guid? ActivityId => null;
        public override Guid? RelatedActivityId => null;
        public override int? ProcessId => 100;
        public override int? ThreadId => 200;
        public override string LevelDisplayName => "Information";
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public override int? Qualifiers => null;
        public override long? RecordId => 1234;
        public override byte? Version => 1;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark {
            get {
                BookmarkCalls++;
                return null!;
            }
        }

        public override string FormatDescription() {
            FormatDescriptionCalls++;
            return "Subject\r\nKey: Value";
        }

        public override string FormatDescription(IEnumerable<object> values) {
            return FormatDescription();
        }

        public override string ToXml() {
            ToXmlCalls++;
            return "<Event><EventData><Data Name='Field'>StructuredValue</Data><Data Name='Payload' Type='Binary'>0102FF</Data></EventData></Event>";
        }

        protected override void Dispose(bool disposing) {
            Disposed = true;
        }
    }
}
