namespace EventViewerX.Rules.Kerberos;

/// <summary>
/// Kerberos policy configuration change event details.
/// </summary>
public class KerberosPolicyChange : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4713 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.KerberosPolicyChange;

    /// <summary>Accepts Kerberos policy change events.</summary>
    public override bool CanHandle(EventObject eventObject)
    {
        return true;
    }
    /// <summary>Domain controller where the policy change occurred.</summary>
    public string Computer;
    /// <summary>Account that modified the policy.</summary>
    public string Who;
    /// <summary>Raw policy change string from the event.</summary>
    public string PolicyChanges;
    /// <summary>Kerberos proxy lifetime in minutes.</summary>
    public double? KerProxyMinutes;
    /// <summary>Kerberos max renewable lifetime in days.</summary>
    public double? KerMaxRDays;
    /// <summary>Kerberos max ticket lifetime in hours.</summary>
    public double? KerMaxTHours;
    /// <summary>Kerberos min ticket lifetime in minutes.</summary>
    public double? KerMinTMinutes;
    /// <summary>Whether user logon restrictions are enforced.</summary>
    public bool? EnforceUserLogonRestrictions;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises a Kerberos policy change wrapper from an event record.</summary>
    public KerberosPolicyChange(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "KerberosPolicyChange";
        Computer = SourceEvent.ComputerName;
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        PolicyChanges = SourceEvent.GetValueFromDataDictionary("KerberosPolicyChange");
        When = SourceEvent.TimeCreated;

        if (!string.IsNullOrEmpty(PolicyChanges) && PolicyChanges != "--")
        {
            foreach (var part in PolicyChanges.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var section = part.Trim();
                if (section.Length == 0) continue;
                var kv = section.Split(new[] { ':' }, 2);
                if (kv.Length != 2) continue;
                var name = kv[0].Trim();
                var valuePart = kv[1];
                var hexMatch = System.Text.RegularExpressions.Regex.Match(valuePart, "0x([0-9A-Fa-f]+)");
                if (!hexMatch.Success) continue;
                var newValue = Convert.ToInt64(hexMatch.Groups[1].Value, 16);

                switch (name)
                {
                    case "KerProxy":
                        KerProxyMinutes = newValue / 600000000d;
                        break;
                    case "KerMaxR":
                        KerMaxRDays = newValue / 864000000000d;
                        break;
                    case "KerMaxT":
                        KerMaxTHours = newValue / 36000000000d;
                        break;
                    case "KerMinT":
                        KerMinTMinutes = newValue / 600000000d;
                        break;
                    case "KerOpts":
                        EnforceUserLogonRestrictions = newValue == 0x80;
                        break;
                }
            }
        }
    }
}



