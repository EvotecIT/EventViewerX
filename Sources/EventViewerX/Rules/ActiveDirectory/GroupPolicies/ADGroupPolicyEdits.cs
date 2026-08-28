namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Represents edits made to group policy objects.
/// </summary>
public class ADGroupPolicyEdits : EventRuleBase {
    /// <inheritdoc />
    public override int MatchPriority => 300;

    /// <summary>Computer where the edit occurred.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Class of the object modified.</summary>
    public string ObjectClass;
    /// <summary>Operation type value.</summary>
    public string OperationType;
    /// <summary>User performing the edit.</summary>
    public string Who;
    /// <summary>Time the edit occurred.</summary>
    public DateTime When;
    /// <summary>Display name of the GPO.</summary>
    public string GroupPolicyDisplayName;
    /// <summary>LDAP display name of the attribute.</summary>
    public string AttributeLDAPDisplayName;
    //public string AttributeValue;
    /// <summary>Resolved group policy information for the edited object (when available).</summary>
    public GroupPolicy GroupPolicy { get; set; } = new GroupPolicy();
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5136, 5137, 5141 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADGroupPolicyEdits;

    /// <summary>Accepts versionNumber attribute edits on groupPolicyContainer objects.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Check if this is a group policy container with versionNumber attribute
        if (eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
            objectClass.Equals("groupPolicyContainer", StringComparison.OrdinalIgnoreCase)) {
            if (eventObject.TryGetDataValue("AttributeLDAPDisplayName", out var ldapDisplayNameValue) &&
                ldapDisplayNameValue.Equals("versionNumber", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>Initialises a GPO edit wrapper from an event record.</summary>
    public ADGroupPolicyEdits(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADGroupPolicyEdits";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        ObjectClass = SourceEvent.GetValueFromDataDictionary("ObjectClass");
        OperationType = ConvertFromOperationType(SourceEvent.GetDataValueOrEmpty("OperationType"));
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
        GroupPolicyDisplayName = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        var dn = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        var guidPattern = @"\{(?<guid>[0-9A-Fa-f-]+)\}";
        var match = System.Text.RegularExpressions.Regex.Match(dn, guidPattern);
        if (match.Success) {
            GroupPolicy = new GroupPolicy {
                GpoId = match.Groups["guid"].Value
            };
        }
        AttributeLDAPDisplayName = SourceEvent.GetValueFromDataDictionary("AttributeLDAPDisplayName");
        //AttributeValue = SourceEvent.GetValueFromDataDictionary("AttributeValue");
    }
}

//- <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
// - <System>
//   <Provider Name="Microsoft-Windows-Security-Auditing" Guid="{54849625-5478-4994-a5ba-3e3b0328c30d}" />
//   <EventID>5136</EventID>
//   <Version>0</Version>
//   <Level>0</Level>
//   <Task>14081</Task>
//   <Opcode>0</Opcode>
//   <Keywords>0x8020000000000000</Keywords>
//   <TimeCreated SystemTime="2024-12-26T18:30:31.5587483Z" />
//   <EventRecordID>164758670</EventRecordID>
//   <Correlation ActivityID="{7818f1df-8631-4326-bfed-1795848cb8ec}" />
//   <Execution ProcessID="712" ThreadID="4720" />
//   <Channel>Security</Channel>
//   <Computer>AD1.ad.evotec.xyz</Computer>
//   <Security />
//   </System>
// - <EventData>
//   <Data Name="OpCorrelationID">{d3c9f198-2c49-4d0f-bc37-465d5fbe9d80}</Data>
//   <Data Name="AppCorrelationID">-</Data>
//   <Data Name="SubjectUserSid">S-1-5-21-853615985-2870445339-3163598659-1105</Data>
//   <Data Name="SubjectUserName">przemyslaw.klys</Data>
//   <Data Name="SubjectDomainName">EVOTEC</Data>
//   <Data Name="SubjectLogonId">0x2ac85901</Data>
//   <Data Name="DSName">ad.evotec.xyz</Data>
//   <Data Name="DSType">%%14676</Data>
//   <Data Name="ObjectDN">CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz</Data>
//   <Data Name="ObjectGUID">{9b263379-4310-4585-9eb3-ee688590d3f0}</Data>
//   <Data Name="ObjectClass">groupPolicyContainer</Data>
//   <Data Name="AttributeLDAPDisplayName">versionNumber</Data>
//   <Data Name="AttributeSyntaxOID">2.5.5.9</Data>
//   <Data Name="AttributeValue">0</Data>
//   <Data Name="OperationType">%%14674</Data>
//   </EventData>
//   </Event>



