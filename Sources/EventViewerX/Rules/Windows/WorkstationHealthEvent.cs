using System.Globalization;

namespace EventViewerX.Rules.Windows;

/// <summary>Common metadata for workstation application, service, storage, hardware and performance events.</summary>
public abstract class WorkstationHealthEvent : EventRuleBase {
    /// <summary>Initializes a projection from the original event snapshot.</summary>
    protected WorkstationHealthEvent(EventObject source, string typeName) : base(source) {
        TypeName = typeName;
        Computer = source.ComputerName;
        When = source.TimeCreated;
    }

    /// <summary>Computer that emitted the event.</summary>
    public string Computer;
    /// <summary>Time the event was recorded. Performance incident timestamps are exposed separately.</summary>
    public DateTime When;

    /// <summary>Accepts only the declared event IDs, providers and original channel.</summary>
    public override bool CanHandle(EventObject source) => source != null &&
        EventIds.Contains(source.Id) && RuleHelpers.IsChannel(source, LogName) &&
        ProviderNames.Any(provider => RuleHelpers.IsProvider(source, provider));

    /// <summary>Reads the first nonempty payload field, including classic unnamed insertion strings.</summary>
    protected string Value(params string[] keys) {
        foreach (string key in keys) {
            if (SourceEvent.TryGetDataValue(key, out string value) && !string.IsNullOrWhiteSpace(value)) {
                return value;
            }
        }
        return string.Empty;
    }

    /// <summary>Reads a nonnegative decimal counter or millisecond duration; missing and invalid values remain unknown.</summary>
    protected long? Number(params string[] keys) => long.TryParse(Value(keys),
        NumberStyles.None, CultureInfo.InvariantCulture, out long value) ? value : null;

    /// <summary>Reads a Windows boolean without treating missing values as false.</summary>
    protected bool? Boolean(string key) => Value(key) switch {
        "true" or "True" or "1" => true,
        "false" or "False" or "0" => false,
        _ => null
    };
}
