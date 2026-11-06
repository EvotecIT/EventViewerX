using Xunit;

namespace EventViewerX.Tests;

public sealed class TestArbitraryObjectPredicates {
    [Theory]
    [InlineData(EventPredicateOperator.IsNull, true)]
    [InlineData(EventPredicateOperator.IsNotNull, false)]
    public void MissingArbitraryMemberHasTheSameNullSemanticsAsAMissingDictionaryField(
        EventPredicateOperator operation, bool expected) {
        EventPredicate predicate = EventPredicate.Compare("Missing", operation);
        Assert.Equal(expected, EventPredicateEvaluator.Matches(predicate, new { Present = 1 }));
        Assert.Equal(expected, EventPredicateEvaluator.Matches(predicate, new Dictionary<string, object?>()));
    }
}
