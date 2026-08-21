namespace PSEventViewer;

/// <summary>PowerShell-friendly view over the canonical EventViewerX typed predicate builder.</summary>
public sealed class PowerShellEventPredicateBuilder {
    private readonly EventPredicateBuilder _builder;

    internal PowerShellEventPredicateBuilder(
        EventPredicateBuilder builder,
        EventType? type = null,
        EventDefinition? definition = null) {

        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        Type = type;
        Definition = definition;
        var fields = new PSObject();
        foreach (EventPredicateField field in builder.Fields) {
            fields.Properties.Add(new PSNoteProperty(field.Name, field));
            foreach (string alias in field.Definition.Aliases) {
                if (fields.Properties[alias] == null) {
                    fields.Properties.Add(new PSAliasProperty(alias, field.Name));
                }
            }
        }
        Fields = fields;
    }

    /// <summary>Stable definition name.</summary>
    public string DefinitionName => _builder.DefinitionName;

    /// <summary>Human-friendly definition label.</summary>
    public string DisplayName => _builder.DisplayName;

    /// <summary>Tab-discoverable typed field builders.</summary>
    public PSObject Fields { get; }

    /// <summary>Current reusable predicate selected through AllOf, AnyOf, Not, or Use.</summary>
    public EventPredicate? Predicate { get; private set; }

    /// <summary>Built-in event type owned by this filter, when applicable.</summary>
    public EventType? Type { get; }

    /// <summary>Custom event definition owned by this filter, when applicable.</summary>
    public EventDefinition? Definition { get; }

    /// <summary>Gets one field by stable name or alias.</summary>
    public EventPredicateField Field(string name) => _builder.Field(name);

    /// <summary>Selects a root predicate that requires every supplied predicate to match.</summary>
    public void AllOf(params EventPredicate[] predicates) =>
        Use(_builder.AllOf(predicates));

    /// <summary>Selects a root predicate that requires at least one supplied predicate to match.</summary>
    public void AnyOf(params EventPredicate[] predicates) =>
        Use(_builder.AnyOf(predicates));

    /// <summary>Selects a root predicate that negates the supplied predicate.</summary>
    public void Not(EventPredicate predicate) =>
        Use(_builder.Not(predicate));

    /// <summary>Selects one already-built predicate as this filter's reusable root.</summary>
    public void Use(EventPredicate predicate) {
        Predicate = _builder.Normalize(predicate ?? throw new ArgumentNullException(nameof(predicate)));
    }

    /// <summary>Returns the selected reusable predicate.</summary>
    public EventPredicate Build() => Predicate ?? throw new InvalidOperationException(
        "No predicate has been selected. Call AllOf, AnyOf, Not, or Use first.");

    /// <summary>Explains the selected predicate's native and managed execution stages.</summary>
    public EventPredicatePlan Explain() {
        EventPredicate predicate = Build();
        return Definition != null
            ? EventDefinitionEngine.PlanPredicate(Definition, predicate)
            : EventPredicatePlanner.Plan(predicate);
    }
}
