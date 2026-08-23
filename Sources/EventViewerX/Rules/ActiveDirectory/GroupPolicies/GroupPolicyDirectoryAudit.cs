namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>Source-neutral Group Policy directory audit event before optional persistent-context resolution.</summary>
public sealed class GroupPolicyDirectoryAudit : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5136, 5137, 5139, 5141 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.GroupPolicyDirectoryAudit;

    /// <summary>Handles Group Policy containers, scope links/inheritance, and WMI-filter objects or assignments.</summary>
    public override bool CanHandle(EventObject eventObject) {
        string objectClass = eventObject.GetDataValueOrEmpty("ObjectClass");
        string attribute = eventObject.GetDataValueOrEmpty("AttributeLDAPDisplayName");
        return string.Equals(objectClass, "groupPolicyContainer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(objectClass, "msWMI-Som", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(attribute, "gPLink", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(attribute, "gPOptions", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(attribute, "gPCWQLFilter", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Creates the typed projection.</summary>
    public GroupPolicyDirectoryAudit(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = nameof(GroupPolicyDirectoryAudit);
        ObjectDistinguishedName = First("NewObjectDN", "ObjectDN", "OldObjectDN");
        OldObjectDistinguishedName = SourceEvent.GetDataValueOrEmpty("OldObjectDN");
        NewObjectDistinguishedName = SourceEvent.GetDataValueOrEmpty("NewObjectDN");
        ObjectGuid = SourceEvent.GetDataValueOrEmpty("ObjectGUID");
        ObjectClass = SourceEvent.GetDataValueOrEmpty("ObjectClass");
        AttributeName = SourceEvent.GetDataValueOrEmpty("AttributeLDAPDisplayName");
        AttributeValue = SourceEvent.GetDataValueOrEmpty("AttributeValue");
        OperationType = SourceEvent.GetDataValueOrEmpty("OperationType");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        OperationCorrelationId = SourceEvent.GetDataValueOrEmpty("OpCorrelationID");
        ApplicationCorrelationId = SourceEvent.GetDataValueOrEmpty("AppCorrelationID");
        When = SourceEvent.TimeCreated;
    }

    /// <summary>Affected object distinguished name.</summary>
    public string ObjectDistinguishedName = string.Empty;
    /// <summary>Previous distinguished name for a move.</summary>
    public string OldObjectDistinguishedName = string.Empty;
    /// <summary>New distinguished name for a move.</summary>
    public string NewObjectDistinguishedName = string.Empty;
    /// <summary>Affected directory object GUID.</summary>
    public string ObjectGuid = string.Empty;
    /// <summary>Affected LDAP object class.</summary>
    public string ObjectClass = string.Empty;
    /// <summary>Changed LDAP attribute.</summary>
    public string AttributeName = string.Empty;
    /// <summary>Raw changed value.</summary>
    public string AttributeValue = string.Empty;
    /// <summary>Raw directory operation resource value.</summary>
    public string OperationType = string.Empty;
    /// <summary>Account that made the change.</summary>
    public string Who = string.Empty;
    /// <summary>Directory operation correlation identifier.</summary>
    public string OperationCorrelationId = string.Empty;
    /// <summary>Application correlation identifier.</summary>
    public string ApplicationCorrelationId = string.Empty;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    private string First(params string[] names) {
        foreach (string name in names) {
            string value = SourceEvent.GetDataValueOrEmpty(name);
            if (!string.IsNullOrWhiteSpace(value)) {
                return value;
            }
        }
        return string.Empty;
    }
}
