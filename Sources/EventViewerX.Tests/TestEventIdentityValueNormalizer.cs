using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventIdentityValueNormalizer {
    [Theory]
    [InlineData("ProcessId")]
    [InlineData("ClientProcessId")]
    [InlineData("SessionId")]
    public void IdentifierFieldsEndingInSIdAreNotTreatedAsSecurityIdentifiers(string fieldName) {
        EventNormalizedValue normalized = EventValueNormalizerRegistry.Normalize(new EventValueContext {
            FieldName = fieldName,
            RawValue = "0x300"
        });

        Assert.Equal("identity", normalized.Normalizer);
        Assert.Equal(EventNormalizationOutcome.Unchanged, normalized.Outcome);
        Assert.Equal(EventNormalizedValueKind.Text, normalized.Kind);
        Assert.Empty(normalized.Warnings);
    }

    [Theory]
    [InlineData("SubjectUserSid")]
    [InlineData("TargetUserSID")]
    [InlineData("subject_user_sid")]
    public void ExplicitSidFieldsRetainSecurityIdentifierNormalization(string fieldName) {
        EventNormalizedValue normalized = EventValueNormalizerRegistry.Normalize(new EventValueContext {
            FieldName = fieldName,
            RawValue = "s-1-5-18"
        });

        Assert.Equal("event-identity", normalized.Normalizer);
        Assert.Equal(3, normalized.NormalizerVersion);
        Assert.Equal(EventNormalizedValueKind.SecurityIdentifier, normalized.Kind);
        Assert.Equal(EventNormalizationOutcome.Normalized, normalized.Outcome);
        Assert.Equal("S-1-5-18", normalized.Value);
        Assert.Empty(normalized.Warnings);
    }
}
