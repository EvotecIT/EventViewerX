using System;

namespace EventViewerX.Rules.ActiveDirectory {
    /// <summary>
    /// Represents detailed organizational unit change events.
    /// </summary>
    public class ADOrganizationalUnitChangeDetailed : EventRuleBase {


        //        ADOrganizationalUnitChangesDetailed = [ordered] @{
        //        Enabled        = $false
        //        OUEventsModify = @{
        //            Enabled          = $true
        //            Events           = 5136, 5137, 5139, 5141
        //            LogName          = 'Security'
        //            Filter           = [ordered] @{
        //                'ObjectClass' = 'organizationalUnit'
        //            }
        //            Functions        = @{
        //                'OperationType' = 'ConvertFrom-OperationType'
        //            }

        //            Fields           = [ordered] @{
        //                'Computer'                 = 'Domain Controller'
        //                'Action'                   = 'Action'
        //                'OperationType'            = 'Action Detail'
        //                'Who'                      = 'Who'
        //                'Date'                     = 'When'
        //                'ObjectDN'                 = 'Organizational Unit'
        //                'AttributeLDAPDisplayName' = 'Field Changed'
        //                'AttributeValue'           = 'Field Value'
        //                #'OldObjectDN'              = 'OldObjectDN'
        //                #'NewObjectDN'              = 'NewObjectDN'
        //                # Common Fields
        //                'RecordID'                 = 'Record ID'
        //                'ID'                       = 'Event ID'
        //                'GatheredFrom'             = 'Gathered From'
        //                'GatheredLogName'          = 'Gathered LogName'
        //            }
        //            Overwrite        = [ordered] @{
        //                'Action Detail#1' = 'Action', 'A directory service object was created.', 'Organizational Unit Created'
        //                'Action Detail#2' = 'Action', 'A directory service object was deleted.', 'Organizational Unit Deleted'
        //                'Action Detail#3' = 'Action', 'A directory service object was moved.', 'Organizational Unit Moved'
        //                #'Organizational Unit' = 'Action', 'A directory service object was moved.', 'OldObjectDN'
        //                #'Field Changed'       = 'Action', 'A directory service object was moved.', ''
        //                #'Field Value'         = 'Action', 'A directory service object was moved.', 'NewObjectDN'
        //            }
        //            # This Overwrite works in a way where you can swap one value with another value from another field within same Event
        //            # It's useful if you have an event that already has some fields used but empty and you wnat to utilize them
        //            # for some content
        //            OverwriteByField = [ordered] @{
        //                'Organizational Unit' = 'Action', 'A directory service object was moved.', 'OldObjectDN'
        //                #'Field Changed'       = 'Action', 'A directory service object was moved.', ''
        //                'Field Value'         = 'Action', 'A directory service object was moved.', 'NewObjectDN'
        //            }
        //            SortBy           = 'Record ID'
        //            Descending       = $false
        //            IgnoreWords      = @{ }
        //        }
        //        }
        /// <summary>Domain controller where the OU event originated.</summary>
        public string Computer;
        /// <summary>Short description of the OU change.</summary>
        public string Action;
        /// <summary>Translated action description.</summary>
        public string OperationType;
        /// <summary>Account that performed the change.</summary>
        public string Who;
        /// <summary>Timestamp of the change.</summary>
        public DateTime When;
        /// <summary>Distinguished name of the OU affected.</summary>
        public string OrganizationalUnit; // 'User Object'
        /// <summary>Attribute that was modified.</summary>
        public string FieldChanged; // 'Field Changed'
        /// <summary>Value written to the attribute.</summary>
        public string FieldValue; // 'Field Value'
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5136, 5137, 5139, 5141 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADOrganizationalUnitChangeDetailed;

    /// <summary>Handles OU object events except gPLink changes owned by the Group Policy projection.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Group Policy link changes have a dedicated typed projection.
        return eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
               objectClass.Equals("organizationalUnit", StringComparison.OrdinalIgnoreCase) &&
               (!eventObject.TryGetDataValue("AttributeLDAPDisplayName", out var attrName) ||
                !attrName.Equals("gPLink", StringComparison.OrdinalIgnoreCase));
    }


        /// <summary>Initialises a detailed OU change wrapper from an event record.</summary>
        public ADOrganizationalUnitChangeDetailed(EventObject eventObject) : base(eventObject) {
            // common fields
            SourceEvent = eventObject;
            TypeName = "ADOrganizationalUnitChangeDetailed";
            Computer = SourceEvent.ComputerName;
            Action = SourceEvent.MessageSubject;
            Who = SourceEvent.GetSubjectAccountOrEmpty();
            When = SourceEvent.TimeCreated;

            OperationType = ConvertFromOperationType(SourceEvent.GetDataValueOrEmpty("OperationType"));
            OrganizationalUnit = SourceEvent.GetValueFromDataDictionary("ObjectDN");
            FieldChanged = SourceEvent.GetValueFromDataDictionary("AttributeLDAPDisplayName");
            FieldValue = SourceEvent.GetValueFromDataDictionary("AttributeValue");
            // OverwriteByField logic
            OrganizationalUnit = OverwriteByField(Action, "A directory service object was moved.", OrganizationalUnit, SourceEvent.GetValueFromDataDictionary("OldObjectDN"));
            FieldValue = OverwriteByField(Action, "A directory service object was moved.", FieldValue, SourceEvent.GetValueFromDataDictionary("NewObjectDN"));

            //OperationType = OverwriteByField(Action, "A directory service object was created.", OperationType, "Organizational Unit Created");
            //OperationType = OverwriteByField(Action, "A directory service object was deleted.", OperationType, "Organizational Unit Deleted");
            //OperationType = OverwriteByField(Action, "A directory service object was moved.", OperationType, "Organizational Unit Moved");

            if (Action == "A directory service object was created.") {
                OperationType = "Organizational Unit Created";
            } else if (Action == "A directory service object was deleted.") {
                OperationType = "Organizational Unit Deleted";
            } else if (Action == "A directory service object was moved.") {
                OperationType = "Organizational Unit Moved";
            }
        }
    }
}



