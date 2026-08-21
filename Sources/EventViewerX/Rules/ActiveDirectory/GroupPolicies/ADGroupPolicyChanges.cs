namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Summary information for group policy change events.
/// </summary>
/// <remarks>
/// This rule focuses on high-level fields (DN, attribute, actor) for GPO/container objects; detailed change deltas are handled by
/// <see cref="ADGroupPolicyChangesDetailed"/>. The <see cref="OperationType"/> value is left as emitted by the provider.
/// </remarks>
public class ADGroupPolicyChanges : EventRuleBase {
    /// <summary>Computer where the change occurred.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Class of the object modified.</summary>
    public string ObjectClass;
    /// <summary>Operation type value.</summary>
    public string OperationType { get; set; } = string.Empty;
    /// <summary>User performing the action.</summary>
    public string Who;
    /// <summary>Timestamp of the event.</summary>
    public DateTime When;
    /// <summary>Distinguished name of the GPO.</summary>
    public string GpoName;
    /// <summary>LDAP display name of the attribute.</summary>
    public string AttributeLDAPDisplayName;
    /// <summary>Value of the attribute.</summary>
    public string AttributeValue;
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5136, 5137, 5141 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADGroupPolicyChanges;

    /// <summary>Handles groupPolicyContainer or container objects in the Security log.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Check if this is a group policy container or container object
        return eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
               (objectClass.Equals("groupPolicyContainer", StringComparison.OrdinalIgnoreCase) ||
                objectClass.Equals("container", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Initialises a group policy change wrapper from an event record.</summary>
    public ADGroupPolicyChanges(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADGroupPolicyChanges";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        ObjectClass = SourceEvent.GetValueFromDataDictionary("ObjectClass");
        // OperationType = ConvertFromOperationType(SourceEvent.Data["OperationType"]);
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
        GpoName = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        AttributeLDAPDisplayName = SourceEvent.GetValueFromDataDictionary("AttributeLDAPDisplayName");
        AttributeValue = SourceEvent.GetValueFromDataDictionary("AttributeValue");
    }
}

//- <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
// - <System>
//   <Provider Name="Microsoft-Windows-Security-Auditing" Guid="{54849625-5478-4994-a5ba-3e3b0328c30d}" />
//   <EventID>5137</EventID>
//   <Version>0</Version>
//   <Level>0</Level>
//   <Task>14081</Task>
//   <Opcode>0</Opcode>
//   <Keywords>0x8020000000000000</Keywords>
//   <TimeCreated SystemTime="2024-12-26T18:30:31.5594768Z" />
//   <EventRecordID>164758673</EventRecordID>
//   <Correlation ActivityID="{3d47ddee-909d-4aef-a324-cca46a47725d}" />
//   <Execution ProcessID="712" ThreadID="4720" />
//   <Channel>Security</Channel>
//   <Computer>AD1.ad.evotec.xyz</Computer>
//   <Security />
//   </System>
// - <EventData>
//   <Data Name="OpCorrelationID">{8e498f88-21d5-4eb1-902a-01c8672e6e0c}</Data>
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
//   </EventData>
//   </Event>


