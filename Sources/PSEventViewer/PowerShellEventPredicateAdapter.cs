namespace PSEventViewer;

/// <summary>Converts PowerShell predicate inputs into the canonical EventViewerX model.</summary>
internal static class PowerShellEventPredicateAdapter {
    internal static EventPredicate? Resolve(
        object? value,
        string parameterName,
        EventPredicateBuilder? builder = null) {

        while (value is PSObject wrapper && wrapper.BaseObject != value) {
            value = wrapper.BaseObject;
        }
        if (value == null) {
            return null;
        }
        EventPredicate predicate = value switch {
            EventPredicate typed => typed,
            PowerShellEventPredicateBuilder filterBuilder => filterBuilder.Build(),
            ScriptBlock scriptBlock => PowerShellEventPredicateAstParser.Parse(scriptBlock, builder),
            string text when File.Exists(text) => EventPredicate.Load(text),
            string text => EventPredicate.ParseJson(text),
            _ => throw new PSArgumentException(
                $"{parameterName} must be a typed filter builder, EventPredicate, restricted ScriptBlock, predicate JSON, or predicate JSON file path.",
                parameterName)
        };
        predicate.Validate();
        return predicate;
    }
}
