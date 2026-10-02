using Xunit;

namespace EventViewerX.Tests;

public sealed class TestSavedEventMetadata {
    [Theory]
    [InlineData(EventReadMode.Metadata)]
    [InlineData(EventReadMode.Message)]
    [InlineData(EventReadMode.StructuredData)]
    [InlineData(EventReadMode.RawXml)]
    [InlineData(EventReadMode.StructuredDataAndMessage)]
    [InlineData(EventReadMode.Full)]
    public void SavedXmlPreservesSecurityIdentityAndQualifiers(EventReadMode mode) {
        const string xml = "<Event><System><Provider Name='Audit'/><EventID Qualifiers='16384'>1</EventID>" +
            "<TimeCreated SystemTime='2026-10-01T12:00:00Z'/><EventRecordID>7</EventRecordID>" +
            "<Channel>Application</Channel><Computer>HostA</Computer><Security UserID='S-1-5-18'/></System></Event>";

        SavedEventRecord saved = SavedEventXmlProjector.Create(xml);
        EventObject projected = saved.ToEventObject("fixture.evtx", mode);

        Assert.Equal((ushort)16384, saved.Qualifiers);
        Assert.Equal("S-1-5-18", saved.UserId);
        Assert.Equal("16384", projected.Qualifiers);
        Assert.Equal("S-1-5-18", projected.UserIdText);
        if (OperatingSystem.IsWindows()) {
            Assert.Equal("S-1-5-18", projected.UserId!.Value);
        }
    }
}