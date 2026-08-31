namespace EventViewerX;

/// <summary>Fluent owner for composing packs, rules, and tuning into one immutable detection plan.</summary>
public sealed class EventDetectionPlanBuilder {
    private readonly List<IEventDetectionRule> _rules = new();
    private EventDetectionTuning? _tuning;

    /// <summary>Adds every validated rule from a versioned pack.</summary>
    public EventDetectionPlanBuilder AddPack(EventDetectionPack pack) {
        if (pack == null) {
            throw new ArgumentNullException(nameof(pack));
        }
        _rules.AddRange(pack.GetRules());
        return this;
    }

    /// <summary>Adds all built-in native detection packs.</summary>
    public EventDetectionPlanBuilder AddBuiltInPacks() {
        foreach (EventDetectionPack pack in EventDetectionCatalog.GetBuiltInPacks()) {
            AddPack(pack);
        }
        return this;
    }

    /// <summary>Adds one rule implementation.</summary>
    public EventDetectionPlanBuilder AddRule(IEventDetectionRule rule) {
        _rules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
        return this;
    }

    /// <summary>Adds one serializable native rule definition.</summary>
    public EventDetectionPlanBuilder AddRule(EventDetectionRuleDefinition definition) {
        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        _rules.Add(new EventDetectionRule(definition));
        return this;
    }

    /// <summary>Sets detached environment tuning.</summary>
    public EventDetectionPlanBuilder WithTuning(EventDetectionTuning? tuning) {
        _tuning = tuning;
        return this;
    }

    /// <summary>Compiles one immutable reusable plan.</summary>
    public EventDetectionPlan Build() => EventDetectionPlan.Compile(_rules, _tuning);
}
