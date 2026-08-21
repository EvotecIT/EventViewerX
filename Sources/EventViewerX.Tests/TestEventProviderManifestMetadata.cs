using EventViewerX.Providers;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventProviderManifestMetadata {
    [Fact]
    public void RejectsUnknownWindowsOpcodeAndKeywordReferences() {
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        definition.Events[0].Opcode = "win:NotAnOpcode";
        definition.Events[0].Keywords.Add("win:NotAKeyword");

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventOpcodeUnknown");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventKeywordUnknown");
    }

    [Fact]
    public void RejectsIncorrectlyCasedWindowsMetadataReferences() {
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        definition.Events[0].Level = "win:informational";
        definition.Events[0].Opcode = "win:start";
        definition.Events[0].Keywords.Add("win:auditsuccess");

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventLevelUnknown");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventOpcodeUnknown");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventKeywordUnknown");
    }

    [Fact]
    public void AcceptsKnownWindowsOpcodeAndKeywordReferences() {
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        definition.Events[0].Opcode = "win:Start";
        definition.Events[0].Keywords.Add("win:AuditSuccess");

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsReservedZeroTaskValue() {
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        definition.Tasks.Add(new EventProviderTaskDefinition {
            Name = "Scan",
            Value = 0
        });

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        EventProviderValidationIssue issue = Assert.Single(
            result.Errors,
            static item => item.Code == "CustomTaskReserved");
        Assert.Equal("Tasks[0].Value", issue.Path);
    }

    [Theory]
    [InlineData("win:WDIContext")]
    [InlineData("win:WDIDiag")]
    [InlineData("win:EventlogClassic")]
    public void AcceptsCanonicalWindowsKeywordReferences(
        string keyword) {

        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        definition.Events[0].Keywords.Add(keyword);

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsCaseMismatchedCustomMetadataReferences() {
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        definition.Levels.Add(
            new EventProviderLevelDefinition {
                Name = "Detailed",
                Value = 16
            });
        definition.Tasks.Add(
            new EventProviderTaskDefinition {
                Name = "ScanTask",
                Value = 1
            });
        definition.Opcodes.Add(
            new EventProviderOpcodeDefinition {
                Name = "ScanOpcode",
                Value = 10
            });
        definition.Keywords.Add(
            new EventProviderKeywordDefinition {
                Name = "ScanKeyword",
                Mask = 1
            });
        definition.Maps.Add(
            new EventProviderMapDefinition {
                Name = "ScanMap"
            });
        EventProviderEventDefinition eventDefinition =
            definition.Events[0];
        eventDefinition.Channel = "operational";
        eventDefinition.Level = "detailed";
        eventDefinition.Task = "scantask";
        eventDefinition.Opcode = "scanopcode";
        eventDefinition.Keywords.Add("scankeyword");
        eventDefinition.Fields[1].Map = "scanmap";

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventChannelUnknown");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventLevelUnknown");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventTaskUnknown");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventOpcodeUnknown");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventKeywordUnknown");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "FieldMapUnknown");
    }

    [Theory]
    [InlineData(EventProviderFieldType.Guid, "win:GUID")]
    [InlineData(EventProviderFieldType.FileTime, "win:FILETIME")]
    [InlineData(EventProviderFieldType.SystemTime, "win:SYSTEMTIME")]
    [InlineData(EventProviderFieldType.Sid, "win:SID")]
    public void UsesCanonicalWindowsInputTypeNames(
        EventProviderFieldType type,
        string expected) {

        Assert.Equal(
            expected,
            EventProviderManifestNames.TypeName(type));
    }

    [Fact]
    public void GeneratesFallbackForPayloadsBeyondMessageInsertionLimit() {
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        EventProviderEventDefinition eventDefinition =
            definition.Events[0];
        eventDefinition.Fields.Clear();
        for (int index = 1; index <= 128; index++) {
            eventDefinition.Fields.Add(
                new EventProviderFieldDefinition {
                    Name = "Field" + index,
                    Type = EventProviderFieldType.UInt32
                });
        }
        eventDefinition.Messages["en-US"] =
            "First={Field1}.";
        string manifest = EventProviderManifestGenerator.Generate(
            definition,
            "provider.resources.dll");
        string fallback =
            EventProviderManifestGenerator.CreateFallbackEventMessage(
                eventDefinition);
        string compiledFallback =
            EventProviderMessageTemplateCompiler.Compile(
                fallback,
                eventDefinition.Fields);

        Assert.Contains("First=%1.", manifest);
        Assert.Contains(
            "additional fields omitted.",
            compiledFallback);
    }
}
