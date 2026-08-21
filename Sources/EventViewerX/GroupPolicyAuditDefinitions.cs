namespace EventViewerX;

/// <summary>Built-in declarative definitions for source-neutral Group Policy auditing.</summary>
public static class GroupPolicyAuditDefinitions {
    /// <summary>
    /// Creates a definition for Directory Service Changes events that affect a Group Policy container,
    /// a scope link list, a scope inheritance boundary, or a WMI filter.
    /// </summary>
    public static EventDefinition CreateDirectoryChanges() => new() {
        Name = "GroupPolicyDirectoryChanges",
        DisplayName = "Group Policy directory changes",
        Description = "Group Policy object, scope-link, inheritance, and WMI-filter changes from Security auditing.",
        Category = "Active Directory",
        Sources = new[] {
            new EventDefinitionSource {
                LogName = "Security",
                EventIds = new[] { 5136, 5137, 5139, 5141 },
                ProviderNames = new[] { "Microsoft-Windows-Security-Auditing" }
            }
        },
        Fields = new[] {
            Data("ObjectDistinguishedName", "ObjectDN"),
            Data("OldObjectDistinguishedName", "OldObjectDN"),
            Data("NewObjectDistinguishedName", "NewObjectDN"),
            Data("ObjectGuid", "ObjectGUID"),
            Data("ObjectClass", "ObjectClass"),
            Data("AttributeName", "AttributeLDAPDisplayName"),
            Data("AttributeValue", "AttributeValue"),
            Data("OperationType", "OperationType"),
            Data("ActorSid", "SubjectUserSid"),
            Data("ActorUserName", "SubjectUserName"),
            Data("ActorDomainName", "SubjectDomainName"),
            Data("ActorLogonId", "SubjectLogonId"),
            Data("DirectoryServiceName", "DSName"),
            Data("DirectoryServiceType", "DSType"),
            Data("OperationCorrelationId", "OpCorrelationID"),
            Data("ApplicationCorrelationId", "AppCorrelationID")
        }
    };

    private static EventDefinitionField Data(string name, string sourceName) => new() {
        Name = name,
        Source = EventFieldSource.Data,
        SourceName = sourceName
    };
}
