using System.Security.AccessControl;

namespace EventViewerX;

internal static class EventLogSecurityDescriptor {
    internal static EventLogParsedSecurityDescriptor Parse(string sddl) {
        var descriptor = new RawSecurityDescriptor(sddl);
        if ((descriptor.ControlFlags & ControlFlags.DiscretionaryAclPresent) == 0) {
            return new EventLogParsedSecurityDescriptor(EventLogDaclState.NotPresent, Array.Empty<EventLogAccessRule>());
        }

        RawAcl? dacl = descriptor.DiscretionaryAcl;
        if (dacl == null) {
            return new EventLogParsedSecurityDescriptor(EventLogDaclState.Null, Array.Empty<EventLogAccessRule>());
        }
        if (dacl.Count == 0) {
            return new EventLogParsedSecurityDescriptor(EventLogDaclState.Empty, Array.Empty<EventLogAccessRule>());
        }

        var rules = new List<EventLogAccessRule>(dacl.Count);
        foreach (GenericAce ace in dacl) {
            if (ace is not QualifiedAce qualified || ace is not KnownAce known) {
                throw new NotSupportedException($"Event-log DACL contains unsupported ACE type '{ace.AceType}'.");
            }

            rules.Add(new EventLogAccessRule(
                known.SecurityIdentifier.Value,
                ace.AceType.ToString(),
                qualified.AceQualifier == AceQualifier.AccessAllowed,
                qualified.AceQualifier == AceQualifier.AccessDenied,
                (ace.AceFlags & AceFlags.Inherited) != 0,
                known.AccessMask));
        }

        return new EventLogParsedSecurityDescriptor(EventLogDaclState.Entries, rules.AsReadOnly());
    }
}

internal sealed class EventLogParsedSecurityDescriptor {
    internal EventLogParsedSecurityDescriptor(EventLogDaclState daclState, IReadOnlyList<EventLogAccessRule> accessRules) {
        DaclState = daclState;
        AccessRules = accessRules;
    }

    internal EventLogDaclState DaclState { get; }
    internal IReadOnlyList<EventLogAccessRule> AccessRules { get; }
}
