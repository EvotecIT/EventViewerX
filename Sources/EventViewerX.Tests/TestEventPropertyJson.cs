using System.Security.Principal;
using System.Text.Json;
using EventViewerX.Exports;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventPropertyJson {
    [Fact]
    public void SerializesNativePropertyShapesWithoutObjectCycles() {
        var sid = new SecurityIdentifier("S-1-5-18");
        object?[] values = {
            sid,
            Guid.Parse("eb153f00-97c6-4f97-bb52-72f3ef89c5f2"),
            new byte[] { 0, 1, 2, 255 },
            new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Local),
            new object?[] { 42UL, null, true }
        };

        string json = EventPropertyJson.Serialize(values);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.Equal("S-1-5-18", root[0].GetString());
        Assert.Equal("eb153f00-97c6-4f97-bb52-72f3ef89c5f2", root[1].GetString());
        Assert.Equal("AAEC/w==", root[2].GetString());
        Assert.EndsWith("Z", root[3].GetString(), StringComparison.Ordinal);
        Assert.Equal(42UL, root[4][0].GetUInt64());
        Assert.Equal(JsonValueKind.Null, root[4][1].ValueKind);
        Assert.True(root[4][2].GetBoolean());
    }

    [Fact]
    public void SerializesNonFiniteFloatingPointPayloadsWithoutAbortingExport() {
        object?[] values = {
            float.NaN,
            float.PositiveInfinity,
            double.NegativeInfinity,
            1.25D
        };

        string json = EventPropertyJson.Serialize(values);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.Equal("NaN", root[0].GetString());
        Assert.Equal("Infinity", root[1].GetString());
        Assert.Equal("-Infinity", root[2].GetString());
        Assert.Equal(1.25D, root[3].GetDouble());
    }
}
