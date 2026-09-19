using System.Security.AccessControl;

namespace EventViewerX;

internal static class EventLogSecurityDescriptor {
    internal static IReadOnlyList<EventLogAccessRule> ParseAccessRules(string sddl) {
        var descriptor = new RawSecurityDescriptor(sddl);
        RawAcl? dacl = descriptor.DiscretionaryAcl;
        if (dacl == null) {
            return Array.Empty<EventLogAccessRule>();
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

        return rules.AsReadOnly();
    }
}
